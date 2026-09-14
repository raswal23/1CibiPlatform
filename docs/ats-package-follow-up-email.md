# Package follow-up email

## What it does

A package can chase a candidate who never answered their invitation. `PackageDetails.FollowUpEmail`
is a number of days; when that many days have passed since the order was created and the
application form is still unanswered, the candidate is sent **one** reminder email containing
**the same link** they were given the first time. `0` turns it off.

The package form has collected this number for a long time — "Sends a reminder email this many
days after the order is placed. Use 0 to turn it off." — and until this change nothing read it.
The number now does what the hint always claimed.

## Rules

A row is chased only when **all** of these hold:

| Condition | Why |
| --- | --- |
| `AutoChasing IS TRUE` | Same filter the email claim query uses. Data-screening orders have no candidate to email; NULL is excluded deliberately. |
| `FollowUpEmail > 0` | `0` is the off switch. |
| `ApplicationFormStatus = Pending` | Never chase someone who already submitted or withdrew. |
| `EmailSentStatus = Done` | Only chase someone who actually received the first email. A row still queued or failed is not being ignored — it was never delivered. |
| `FollowUpQueuedAt IS NULL` | Fire once, ever. |
| `HashToken IS NOT NULL` | There is a link to resend. |
| `OrderCreatedAt <= now() - FollowUpEmail days` | The interval has elapsed. |

Bounded to 0–90 days by both package validators and the numeric field's `Max`. The chaser fires on
`OrderCreatedAt + interval`, so a mistyped `900` is indistinguishable from "never" until three
years from now.

## Fire once, not a loop

The reminder is sent exactly one time per order. There is no escalating sequence and no second
chaser — a candidate who ignores both emails is the screening team's problem, not the scheduler's.

`FollowUpQueuedAt` is the stamp that guarantees it, and it is written **in the same UPDATE** that
requeues the row. Not in a follow-up write, not in the service layer: if the process dies between
the requeue and the stamp, the next pass would chase the same person again. One statement, no gap.

## The link is reused

The chaser does **not** rotate `HashToken`. This is the entire point — a reminder that carried a
new link would kill the link in the email the candidate already has, which is the opposite of
helping someone who has been meaning to get around to it.

This is why `RequeueEmailInvitationAsync` could not be reused: the manual operator resend rotates
the token, on purpose, because an operator resending usually means "that link is compromised or
lost". The two paths want opposite things, so they are two methods. See `ats-email-delivery.md` §8.

Reusing the link also required removing the 24-hour expiry — a reminder sent on day 3 pointing at
a link that died on day 1 is worse than no reminder. See
`ats-application-form-link-expiry-removal.md`.

## Existing orders are never chased

The migration that adds `FollowUpQueuedAt` backfills every existing row with `now()`, marking the
entire historical backlog as already-chased.

Without that backfill, the first job pass after deploy would look at every open order ever created,
find all of them past their follow-up interval, and send a reminder to all of them at once. Only
orders created **after** the deploy get a follow-up. This is intentional and not recoverable by
re-running anything — if the backlog genuinely should be chased, that is a deliberate one-off
`UPDATE` someone makes with their eyes open.

## The job queues; it never sends

`FollowUpEmailBackgroundJob` runs hourly and does exactly one thing: flip due rows back to
`EmailSentStatus = Pending` and record history. It opens no SMTP connection.

That separation is what keeps reminders inside the email quota. The existing
`EmailNotificationBackgroundJob` picks the rows up on its next 5-second tick and carries them
through the same per-account pool, the same `IsSendable` check, the same rolling-24h
`DailySendLimit`, and the same pacing as every other ATS email. A reminder is not a privileged
message and cannot jump the queue or exceed a cap. If every account is exhausted the rows simply
wait in `Pending` and drain when the window clears.

Hourly is deliberate: the unit here is **days**, so a finer interval is pure database load for a
resolution nobody asked for.

## What the candidate sees

A distinct reminder — its own subject (`CIBI | Reminder: Background Verification Information
Request`) and its own lede, saying plainly that we have not yet received the form, that this is the
same link as before so the original email still works, and to disregard the message if they have
already submitted. Same layout, same instructions, same contact details as the first email.

The sender decides which copy to use from `FollowUpQueuedAt is not null && EmailSentAt is null`,
i.e. "queued by the chaser, not yet delivered". Known and accepted: an operator resend that happens
*after* a chaser also reads as a reminder. That is benign — the candidate has been emailed twice by
then, so "reminder" is accurate — and the alternative, clearing the stamp, would re-arm the chaser,
since `OrderCreatedAt` never moves.

## Order history

Each release records `ApplicationFormFollowUpSent`, shown as "Follow-up reminder sent". It is a
separate event from `ApplicationFormResent` because reading a history, "a person resent this" and
"the schedule did" are different facts.

History is recorded at **queue** time, matching the resend path — it reflects work actually
scheduled, not work confirmed delivered. Delivery success or failure is already visible through
`EmailSentStatus` and the email send log.
