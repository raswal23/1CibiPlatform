# ATS Submitted Form Email

**Scope:** ATS application form — the notice sent when a candidate completes their form. Backend
only; **no** UI, endpoint, route, DTO, entity or migration change.

## What it does

A candidate who opens their emailed application-form link fills in seven sections and submits. That
already saved the form, moved the order to `OrderStatus.InProgress`, wrote an
`ApplicationFormSubmitted` history row, and raised an **in-app** notification for the requestor. No
email went out. This adds it.

| | |
|---|---|
| **To** | the requestor, resolved from the order's `RequestorId` |
| **Cc** | the `SubmittedForm` copy list — `clientsupport@cibi.com.ph`, `pre-workteam@cibi.com.ph` as seeded — then the candidate |
| **Subject** | `CIBI \| Order Status – In Progress` |

The body is the standard CIBI card — navy gradient header, confidentiality footer — greeting the
requestor, naming the candidate, saying the form is complete and available for download through the
ATS, and closing with the contact sentence naming `ccteam@cibi.com.ph` and
`clientsupport@cibi.com.ph`.

This completes the set of three requestor-facing notices: **withdrawn**, **disputed**, and now
**completed**. All three live beside the sender in `Services/EmailService/`, all three are
best-effort after a commit, all three read their subject from a constant in `Constants/`, and all
three read their copy list from a row in `ats."EmailProcessDetails"`.

## How it works

The submit path already existed end to end:

```text
ApplicationFormComponent OnSubmitForm (validate -> assemble seven DTOs)
  -> UI ApplicationFormService.AddApplicationFormDataAsync (multipart POST)
  -> POST ats/addapplicationformdata        (gateway route already registered)
  -> AddApplicationFormDataEndpoint -> AddApplicationFormDataHandler
  -> ATS ApplicationFormService.AddApplicationFormDataAsync
       authorize token -> begin -> save seven sections -> mark form Done
       -> record order history -> commit
       -> raise in-app notification          (pre-existing)
       -> send the completion email          (new)
```

One call was added, immediately after the existing in-app notification. It goes to a new
`ISubmittedFormEmailNotification`, which loads the order row, resolves the requestor's mailbox from
the Auth directory, composes the body through `ATSEmailService`, and sends it via the existing
`IAtsEmailSender` — so it travels the same pooled, capped, paced, failover-capable path as every
other ATS message.

The send goes through `SingleEmailSendRetry.SendAsync`: up to three attempts on the one message, 2s
then 4s apart, and only when the fault is transient. The retry wraps the account switcher from the
outside, so each attempt is a full walk of the registered accounts rather than a second knock on one
of them, and only the send is inside it — the body and the mailbox are resolved above, so attempt 2
re-sends the same message instead of rebuilding it. See
[`ats-email-send-retry`](../ats-email-send-retry/ats-email-send-retry.md). Unlike a queued
invitation, this send has no later pass behind it, so the attempts it gets are the only ones it
gets.

`ats-submitted-form-email_code_explanation.md` walks the chain file by file.

## Order history

The notice writes its own row, `CompletionNoticeEmail`, beside the `ApplicationFormSubmitted` row the
submission itself already wrote. Two rows for one action is deliberate: "the subject completed the
form" and "we told the requestor" are different facts, and the second can fail while the first
already happened.

It records the **attempt**, so the row is written whether or not the email was delivered — a failed
send still appears on the timeline and the reason is in the log. It is *not* written when no send was
attempted at all (no requestor id, the order cannot be found, or the requestor is no longer in the
ATS directory). The status pair is `null → In Progress`; the previous side is null because nothing
moved.

It renders in `OrderStatusHistoryDialog` as "Completion notice", neutral tone. Detail in
`docs/features/ats-order-status-history/`.

## Why it is shaped this way

**It cannot fire twice.** `AuthorizeApplicationFormAsync` (`ApplicationFormService.cs:186`) rejects
any form whose `ApplicationFormStatus` is not `Pending` with a `ConflictException`, and the submit
sets it to `Done`. So a second POST from a double-clicked button or a replayed request never reaches
the notice — no dedupe, idempotency key or "already notified" column is needed.

**After the commit, and it cannot throw.** Beyond the usual reasoning — the submission is durable,
the candidate is not waiting on the email, losing it degrades experience rather than data — there is
a sharper one specific to this method. Its `catch` block deletes **every file the submission
uploaded** as compensation. An exception escaping after `CommitAsync` would therefore destroy the
attachments belonging to a form that is already saved. The notifier wraps itself in
`SideEffectGuard`, the contract the other two notices keep, so nothing can escape.

**The candidate's name comes from the form, not the order.** This is the first message about the
form's contents, so the name the candidate actually typed and signed with is the one that matters —
including when it corrects a typo in the name the order was raised under. The form carries no
primary email address (only `EmailAlternative`), so the candidate's *mailbox* still comes from the
order row, which the notifier loads anyway for the requestor. If the submitted name is blank it
falls back to the stored name, then to the mailbox — the copy reads "Your candidate, \<name\>, has
successfully completed…", so a blank would leave a hole mid-sentence.

**Three addresses copied, and the quota follows.** The `cc` support added for the withdrawal notice
handles this unchanged: `SendThroughAccountAsync` reports `1 + copied.Count`, so a completion notice
charges the sending account four recipients against the seeded row, not one. Four is not a constant
any more — two of the three copies come from the `SubmittedForm` row, so an operator adding or
retiring a mailbox moves the charge with it. The arithmetic is what is fixed; the number is not.

**The follow-up chaser already stops.** `ReleaseDueFollowUpInvitationsAsync` filters
`ApplicationFormStatus = Pending`, so submitting ends the reminders on its own. Nothing to change.

**No link in the body.** The copy says the form is "available for download through the Applicant
Tracking System", which is the console the requestor already signs into. There is no candidate-facing
URL that would be safe to include in a message copied to two internal teams, so none is rendered.
That argument now rests on a row an operator can edit: the reason no link is rendered is that the
message goes to mailboxes the candidate is also on, and who those are is no longer fixed at compile
time. Adding a link here would need the copy list re-read, not just the body.

## How to verify

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~SubmittedForm"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"   # adds Testcontainers; Docker must be running
dotnet build 1CibiPlatform.sln
```

Manual, end to end: open an invitation link, complete all seven sections, sign, and submit. Confirm
the success state renders, that the requestor receives `CIBI | Order Status – In Progress` with the candidate and every
address in the `SubmittedForm` row of `ats."EmailProcessDetails"` on `Cc:` — read the row first
rather than expecting three, since it is operator-editable — that the row reads `ApplicationFormStatus = Done` /
`OrderStatus = InProgress` with `FormCompletedAt` set, and that an `ApplicationFormSubmitted`
history row exists. Then submit the same link again and confirm it is rejected as already completed
— and that **no second email** arrives. Finally, point the sender at a dead account and confirm the
form is still saved with its attachments intact.

A successful SMTP send is not covered by unit tests and cannot be: `SmtpLease` wraps a real
`SmtpClient`. The tests stop at `IAtsEmailSender`, and the composed body is asserted directly since
that is a pure function. The `Cc:` header and the quota charge need the manual pass above — four
recipients against the seeded row, `1 + copied.Count` against whatever the row holds now.

## What not to do

- **Do not move the send before the commit, and do not remove its `SideEffectGuard`.** The
  compensation delete in this method's `catch` makes an escaping exception actively destructive —
  it would remove uploaded files from a saved form.
- **Do not add an "already notified" flag.** `ApplicationFormStatus` already makes the submit
  single-use; a second guard would be redundant state that can disagree with the first.
- **Do not take the candidate's name from the order row.** The submitted name is the point of this
  message; the row is only the fallback for a form with blank name fields.
- **Do not add a primary email field to `PersonalDetailsDTO`** to source the candidate's address
  from the form. The address the invitation link was sent to is the one known to work, and the row
  is loaded regardless.
- **Do not change the subject without the header.** The body's `<h1>` reads
  `SubmittedFormEmail.Subject`, the same constant sent as the subject line.
- **Do not assume the body's contact sentence matches the CC list.** The sentence names
  `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`; the seeded row copies `clientsupport` and
  `pre-workteam`. So `clientsupport` is on both sides, `ccteam` is in the text but has never been
  copied, and `pre-workteam` is copied but unnamed. That mismatch is in the agreed copy and is
  reproduced deliberately — see the wiring table in the code walkthrough before changing either side.
  The two sides can now drift further on their own: the sentence is compiled into `ATSEmailService`
  and the list is a database row, so an operator editing the row changes one side with no commit on
  the other. See [`ats-email-process`](../ats-email-process/ats-email-process.md).
- **Do not put the composer on `IEmailService`**, and do not inject `IAtsEmailSender` into a new ATS
  service without checking the integration host's fake. Both are covered in
  `docs/features/ats-withdrawn-application-email/`.
