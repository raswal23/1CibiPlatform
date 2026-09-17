# ATS Application Form Email Copy

**Scope:** ATS application form — the two candidate-facing emails, the first invitation and the
package follow-up reminder. Backend only; **no** UI, endpoint, route, DTO, entity or migration
change.

## What it does

The invitation and the reminder went to the candidate alone. Every other ATS notice already copies a
CIBI mailbox, so a team could see a withdrawal, a dispute and a completed form — but not the request
that started any of them. This adds the copy to the two messages that were missing it.

| | |
|---|---|
| **To** | the candidate, unchanged |
| **Cc** | `ccteam@cibi.com.ph`, `pre-workteam@cibi.com.ph` — the same two the completed-form notice copies |
| **Subject** | unchanged: `CIBI \| Background Verification Information Request`, or the `Reminder:` variant |
| **Body** | unchanged — both already close by naming `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph` |

Neither body changed. The closing sentence already told the candidate to write to `ccteam`, so a
visible `Cc` to that mailbox agrees with the copy they are reading rather than contradicting it.

This completes the set: **invitation**, **reminder**, **withdrawn**, **disputed** and **completed**
are now all copied to CIBI.

## Decisions

**Cc, not Bcc.** The candidate sees the team addresses. That is the intent — the body names `ccteam`
in prose, so hiding the copy would be the odd choice, and `SubmittedFormEmail` already Cc's the same
two teams on a message the candidate is copied on. It is also the smaller change: the sender's
`SendATSEmailWithResultAsync` already takes a `cc` collection and carries it to `MimeMessage.Cc`; a
Bcc would need a new parameter threaded through four methods and its own daily-cap arithmetic.

**Both the invitation and the reminder.** They share one send method, and the copy list does not
branch on which body is being sent. A team mailbox holds the chase next to the original, which is
what makes the thread readable. The cost is that a follow-up run copies the teams once per chased
order.

**Every send, including bulk.** No exemption for the queued path. See the cap note below.

## The daily cap

Copied addresses are real recipients to the provider. `ATSEmailService.SendThroughAccountAsync`
charges `1 + cc.Count` to the sending account's daily cap and writes it to
`ats."EmailSendLog".RecipientCount`, which is **summed**, not counted.

So each entry in the copy list multiplies what a batch consumes. With two copies:

| Batch | Recipients charged before | After |
|---|---|---|
| 1 order | 1 | 3 |
| 500-row bulk upload | 500 | 1,500 |
| A follow-up run over 200 unanswered orders | 200 | 600 |

Against a 450/day account limit, a 500-row upload already needed more than one account; it now needs
roughly three times the headroom. This was raised and accepted. The failover switcher handles a
capped account by moving to the next one, and an exhausted rotation returns `Throttled`, which defers
the row rather than retiring it — so the effect of under-provisioning is a slower queue, not lost
invitations. **Register enough sender accounts before the next large upload.**

## How it works

The send path already existed end to end; one argument was added to one call.

```text
single order:  InsertEmailInvitationRequestAsync (inside TransactionRunner)
                 -> SendApplicationFormToUserEmailAsync
resend:        ResendApplicationFormAsync requeues the row -> the queue below
bulk:          EmailNotificationProcessorService.SendEmailAsync
follow-up:     FollowUpEmailBackgroundJob releases rows -> the same processor

  all of them -> EndorsementSubmissionService.SendApplicationFormToUserEmailWithResultAsync
                   compose body (invitation or reminder)
                   -> IAtsEmailSender.SendATSEmailWithResultAsync(..., cc: ApplicationFormEmail.CopyTeams)
                      -> the pooled, capped, paced, failover-capable send
```

Every route funnels into one method, so the copy list was added in one place and applies to all of
them. There is no second send site to keep in step.

The copy list is a new constant, `Constants/ApplicationFormEmail.cs`, holding `CopyTeams` and nothing
else — unlike its three siblings, which also hold a `Subject`. Each of those is one notice with one
subject. These are two bodies with two subjects, already held as `InvitationSubject` and
`ReminderSubject` beside the send that picks between them; moving a *pair* of subjects into the
constant would separate them from the only code that chooses, and buy nothing.

**Pre-existing and untouched:** both bodies hardcode their `<h1>` as a duplicate of their subject
rather than interpolating the const, unlike `BuildSubmittedFormNotification` which renders
`{SubmittedFormEmail.Subject}`. All four strings currently agree, but editing a subject means editing
its header too. Unifying them is a change to the bodies and was out of scope here.

`ats-application-form-email-copy_code_explanation.md` walks the chain file by file.

## Degradation

Only the result-aware `IAtsEmailSender` can carry a copy list. `IEmailService.SendATSEmailAsync`, the
BuildingBlocks contract that Auth and the test fakes also implement, has no `cc` parameter, and
widening it would force every implementer to reason about a copy list they have no teams for.

`EndorsementSubmissionService` already guards that cast with `as` and falls back to the bool path
when it fails. On that path the candidate still receives their link; only the copy is lost. Losing
the copy must never lose the send, and a unit test pins it.

In production the keyed `"ats"` registration is always `ATSEmailService`, so the fallback is
unreachable there.

## Manual verification

Unit tests stop at `IAtsEmailSender` — a successful SMTP send cannot be faked without a server. These
need a real pass:

1. Raise a single manual-screening order. Confirm the candidate's inbox shows both team addresses in
   the `Cc` header, and that both team mailboxes received it.
2. Confirm the matching `ats."EmailSendLog"` row has `RecipientCount = 3`, not `1`.
3. Let a package follow-up fire, or release one, and confirm the reminder carries the same `Cc`.
4. Run a small bulk upload and confirm the per-account consumption in the email accounts screen moves
   by three per row.

## Before release

`ApplicationFormEmail.CopyTeams` currently holds a tester's mailboxes, with the real block commented
out directly above it. This matches the same swap already present in `SubmittedFormEmail`,
`WithdrawnEmail` and `DisputeEmail` on this branch.

**All four must be restored together.** The three sibling notice tests
(`SubmittedFormEmailNotificationTests`, `WithdrawnEmailNotificationTests`,
`DisputeEmailNotificationTests`) pin the real addresses as literals and are red until that happens —
they are doing exactly what they were written to do. They go green together when the constants are
restored.
