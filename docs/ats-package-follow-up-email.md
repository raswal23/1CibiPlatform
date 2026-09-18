# Package follow-up email

## What it does

A package can chase a candidate who never answered their invitation. `PackageDetails.FollowUpEmail`
is a **count of daily reminders**; starting the day after the order was created, the candidate is
sent one reminder email a day — containing **the same link** they were given the first time — until
either the form is answered or that many reminders have gone out. `0` turns it off.

Each day's reminder is anchored to the order's own time of day, so an order placed at 8am is chased
at around 8am, once every 24 hours. A package set to `2` on an order placed Sep 16 at 8am sends on
Sep 17 ~8am and Sep 18 ~8am, then stops.

## Rules

A row is chased only when **all** of these hold:

| Condition | Why |
| --- | --- |
| `AutoChasing IS TRUE` | Same filter the email claim query uses. Data-screening orders have no candidate to email; NULL is excluded deliberately. |
| `FollowUpEmail > 0` | `0` is the off switch. |
| `ApplicationFormStatus = Pending` | Never chase someone who already submitted or withdrew. |
| `EmailSentStatus = Done` | Only chase someone who actually received the first email. A row still queued or failed is not being ignored — it was never delivered. |
| `LastFollowUpSentDate IS DISTINCT FROM today` | One per day. `IS DISTINCT FROM`, not `<>`, so a NULL (never chased) passes. |
| `HashToken IS NOT NULL` | There is a link to resend. |
| `now() >= OrderCreatedAt + 1 day` | The first reminder is the day *after* the order, never the same day. |
| `FollowUpSentCount < FollowUpEmail` | **The stop condition** — N reminders *sent*, not N days elapsed. |
| `now() < OrderCreatedAt + (FollowUpEmail + 8) days` | The backstop that keeps the schedule bounded. |

Bounded to 0–90 by both package validators and the numeric field's `Max` on each form.

### The stop condition counts sends, not days

`FollowUpSentCount` is incremented in the same `UPDATE` that stamps `LastFollowUpSentDate`, and the
schedule ends when it reaches `FollowUpEmail`.

It originally ended `FollowUpEmail` days after the order, which assumed a reminder goes out every
day. It does not — a row is only released when it satisfies every rule above, so a day passes with
nothing sent whenever the first invitation is still queued behind the send quota or has failed.
That cut the schedule short for exactly the candidates who had received the least: an order whose
delivery was stuck for three days simply lost three of its reminders. Counting sends means a missed
day is deferred, not forfeited.

### Why the window still exists

`FollowUpSentCount` alone is not time-bounded, so a never-chased order would stay eligible forever.
The window survives as a **backstop**, widened by `FollowUpCatchUpGraceDays` (7) so a realistic
outage can be caught up without letting a months-old order start chasing. This matters more than it
looks: the daily-reminder migration ships no backfill precisely because the window is what protects
the existing backlog — see *Existing orders are never chased* below.

The lower bound still has its `+ 1`: the first reminder is the day *after* the order, so dropping it
would chase on the order date itself.

## Once a day, for N days

The scheduler's job is not "send a reminder" but "send **at most one** reminder per candidate per
day". The job runs hourly, so a row that is due stays due for the rest of its local day — without a
guard, every remaining pass would release it again, up to 24 times.

`LastFollowUpSentDate` is that guard, and it is written **in the same UPDATE** that requeues the
row. Not in a follow-up write, not in the service layer: if the process dies between the requeue
and the stamp, the next hourly pass would chase the same person again the same day. One statement,
no gap.

## All times are Manila, deliberately

The date comparison and the time-of-day comparison both run in `Asia/Manila`, not UTC — the one
place in the ATS module that converts. This is a correctness requirement, not a preference.

Because each send is anchored to the order's time of day, a UTC-dated dedupe double-sends for any
order created between midnight and 8am Manila. An order at Sep 16 06:00 +08 (Sep 15 22:00Z) is
released at Sep 16 22:00Z and stamped with the UTC date Sep 16 — then released **again** two hours
later at Sep 17 00:00Z, because the UTC date rolled over in the middle of the Manila day. Dating in
Manila has no such window: consecutive releases are always a full local day apart.

Safe because the Philippines has had no DST since 1978 (fixed UTC+8), so the conversion is plain
arithmetic with none of the ambiguous or skipped local times that make local-time scheduling
hazardous elsewhere. Postgres ships its own tzdata, so the zone name resolves regardless of the
container's locale.

## Email volume scales with the setting

Under the original one-shot design an unanswered order cost exactly one reminder. It now costs up
to N. A client with 300 open orders on a package set to `5` adds 1,500 sends.

Those sends are not privileged: the chaser only ever moves rows to `Pending`, so they queue behind
the same per-account pool and rolling-24h `DailySendLimit` as first invitations and drain when the
window clears. The practical risk is not a breached quota but *first* invitations waiting behind a
backlog of reminders — worth knowing before setting a high value on a high-volume package.

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

Originally this was guaranteed by a backfill: the migration that added the now-removed
`FollowUpQueuedAt` stamped every existing row with `now()`, marking the whole historical backlog as
already-chased. Under the fire-once design that stamp was the only thing standing between a
brand-new job and every open order in the table.

**The window now does that job instead, which is why neither the daily-reminder migration nor the
`FollowUpSentCount` migration adds a backfill.** An order is only eligible while `now()` is inside
`[order + 1 day, order + (N + 8) days)`. Anything older than its package's reminder count plus the
catch-up grace has fallen out of that window and can never be selected again, whatever
`LastFollowUpSentDate` or `FollowUpSentCount` hold. Leaving those columns NULL and `0` on existing
rows is correct: nothing has been queued or counted.

This is a stronger guarantee than the backfill was, because it is not a one-time write that a later
`UPDATE` could undo — an order past its grace window is structurally ineligible.

Widening the window by the grace period is what made it worth keeping once the stop condition moved
to the count. It is no longer *why* the schedule ends; it is only the bound that stops an uncounted
order from being eligible forever.

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
resolution nobody asked for. It is also the granularity of the send time — a reminder for an 08:00
order goes out on the first pass at or after 08:00, not at 08:00 exactly.

Hourly is *why* `LastFollowUpSentDate` has to exist. Deleting the column while keeping this trigger
means up to 24 reminders per candidate per day.

## What the candidate sees

A distinct reminder — its own subject (`CIBI | Reminder: Background Verification Information
Request`) and its own lede, saying plainly that we have not yet received the form, that this is the
same link as before so the original email still works, and to disregard the message if they have
already submitted. Same layout, same instructions, same contact details as the first email.

The sender decides which copy to use from `LastFollowUpSentDate is not null && EmailSentAt is null`,
i.e. "queued by the chaser, not yet delivered". Both halves are needed: the date stays set once
stamped, so on its own a row whose reminder was already delivered would keep claiming to be one.
`EmailSentAt` is cleared by the same release `UPDATE`, so the pair is what makes it "not yet sent".

This is the same column that gates the release. An earlier design kept a separate `FollowUpQueuedAt`
timestamp purely for this line; it was dropped once the date could answer the question, because a
second stamp was not only redundant but **wrong**. `FollowUpQueuedAt`'s own migration backfilled
every pre-existing row with `now()` to protect the backlog from the then-new chaser, and that
backfill made the copy check lie: an operator resending a legacy order clears `EmailSentAt`, and
with a non-null stamp already present the row read as "queued as a follow-up" — so a candidate who
had never been chased received an email telling them we had not yet received their form.
`LastFollowUpSentDate` has no backfill, precisely because nothing older than its window can be
chased, so null genuinely means never chased.

Known and accepted: an operator resend that happens *after* a real chase still reads as a reminder,
because the resend clears `EmailSentAt` without clearing the date. That is benign — the candidate
has been emailed twice by then, so "reminder" is accurate.

## Order history

Each release records `ApplicationFormFollowUpSent`, shown as "Follow-up reminder sent". It is a
separate event from `ApplicationFormResent` because reading a history, "a person resent this" and
"the schedule did" are different facts.

History is recorded at **queue** time, matching the resend path — it reflects work actually
scheduled, not work confirmed delivered. Delivery success or failure is already visible through
`EmailSentStatus` and the email send log.

Under daily reminders an order accumulates one `ApplicationFormFollowUpSent` event per reminder, so
the history is also the audit trail for how many times a candidate was chased.

### Queued versus delivered

`ApplicationFormFollowUpSent` records that a reminder was **queued**. A second event,
`InvitationEmailSent`, records that an invitation was actually **handed to the SMTP server** — and
it is written for every send: first invitations, operator resends and reminders alike.

The two exist because the gap between them is real. A queued reminder waits for a sender account
with quota left, which on a busy day can be minutes or hours, and a row that never drains leaves a
queue event with no delivery event after it. That difference is the first thing worth knowing when
a candidate says they never received anything.

`InvitationEmailSent` is written **inside the same transaction** as the `EmailSentStatus = Done`
flip it describes, because they are one fact. Written separately, a crash between them leaves an
order showing Done with nothing in its history to say when — and nothing ever revisits a Done row
to notice the gap. Both paths do this: the queued path in `EmailNotificationProcessorService` wraps
the pair in `TransactionRunner.RunAsync`, and the inline path in `EndorsementSubmissionService`
records it inside the transaction that already surrounds the order insert.

This is `TransactionRunner`, not `SideEffectGuard`: the history here is not a best-effort follow-up
to the send, it is the record *of* the send. If it cannot be written, the status must not stand
either — the row stays claimable and the next pass retries both.

## Seeing what is left

The Orders & Reports board carries a **Follow-ups Left** column, so an operator can tell at a
glance whether a silent candidate is still being chased or whether the schedule is spent and
someone needs to pick up the phone.

| Shown | Means |
| --- | --- |
| `2 left` | Two more daily reminders will be sent while the form stays unanswered |
| `Done` | Chasing applied, and every scheduled reminder has been sent |
| `—` | Chasing does not apply: data screening, reminders switched off, or the form already answered or withdrawn |

`Done` and `—` are deliberately different. Collapsing them would report every data-screening order
on the board as though it had been chased and given up on.

The number is computed per request in `ReportService.CalculateFollowUpEmailsRemaining`, not stored
and not computed in SQL — the answer depends on `LastFollowUpSentDate`, which the chaser moves, and
these rows pass through a cache decorator, so a persisted number would be stale by however long the
entry lives.

### It counts sends, not days

`FollowUpEmail − FollowUpSentCount` — the **same subtraction the release query's stop condition
makes**, against the same two columns. That is the point: the board and the chaser cannot drift
apart, because they read one fact rather than two formulas someone has to remember to keep in step.

It used to deduct elapsed days, which quietly assumed the schedule always runs. When delivery was
stuck, the board counted the day anyway: an order placed today showed `2 left`, then `1 left` the
next morning, for a candidate who had received no email at all.

One thing the number cannot express: the release query also refuses orders past the catch-up grace,
so a long-abandoned order can show a non-zero count that will never be sent. Accepted — it only
affects orders already far outside their window, and encoding the grace period in a second place
would reintroduce exactly the duplicated-formula drift this design removes.

A wrong number here is worse than no column at all, because an operator will trust it instead of
chasing the candidate themselves.

## How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~FollowUp"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.IntegrationTests"
dotnet build 1CibiPlatform.sln
```

The integration tests run against a real PostgreSQL Testcontainer, which is the only place
`AT TIME ZONE`, the `date` column and the migration are actually exercised — a green unit run says
nothing about whether the column exists.

Correct looks like: an order seeded one day back with `FollowUpEmail >= 1` releases on the first
call and **not** on an immediate second call (same day); an order seeded on the current day releases
nothing; an order whose `FollowUpSentCount` has reached `FollowUpEmail` releases nothing however
recent it is; an order with sends still owed releases even when it is past its nominal schedule, so
long as it is inside the grace window; and an order past that grace releases nothing however long it
has waited.

## What not to do

| Don't | Because |
| --- | --- |
| Drop `LastFollowUpSentDate` or stop writing it in the release `UPDATE` | The job is hourly. Without the guard a due row is released on every remaining pass of the day — up to 24 reminders. |
| Write the date stamp in a second statement or in the service layer | A crash between the two writes leaves a released, undated row, which the next pass chases again. |
| Switch the comparisons to UTC, or inline a second `"Asia/Manila"` literal | Double-sends for orders created between midnight and 8am Manila, and two literals drift. Both sides use `FollowUpSchedule`. |
| Move `InvitationEmailSent` outside the transaction that sets `EmailSentStatus = Done` | A crash between them leaves a delivered-looking order with no delivery record, and nothing revisits a Done row. |
| Make the stop condition time-based again | Days pass whether or not a reminder was sent, so the schedule ends early for whoever had delivery trouble — the candidates who received the least get chased the fewest times. |
| Increment `FollowUpSentCount` anywhere but the release `UPDATE` | A crash between the release and the increment leaves a sent-but-uncounted row, and the schedule overruns. Same reasoning as the date stamp. |
| Derive the sent count from `LastFollowUpSentDate − OrderCreatedAt` instead of storing it | The gap only equals the count while every reminder lands on its own day. One late catch-up send and it overcounts, ending the schedule early. |
| Remove the window now that the count is the stop condition | It is still the only bound on eligibility. Without it every never-chased order in the table is eligible forever, which is a mass send — and it is why no migration backfills. |
| Stop writing `LastFollowUpSentDate`, or add a second "has been queued" stamp beside it | It does two jobs now — the once-per-day guard *and* the reminder-copy signal. A second stamp is what made the copy check wrong before. |
| Backfill `LastFollowUpSentDate` or `FollowUpSentCount` for existing rows | Null/`0` means "never chased", which is what the copy check and the count read. A backfill would make every legacy resend claim to be a reminder. |
| Remove the `+ 1` on the window's lower bound | The first reminder would fire on the order date itself, rather than the day after. |
| Rotate `HashToken` on release | Kills the link already sitting in the candidate's inbox. |
| Raise `FollowUpEmail` on a high-volume package without checking send capacity | Reminders queue ahead of first invitations for that account. |
