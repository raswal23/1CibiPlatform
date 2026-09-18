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
| **Cc** | `clientsupport@cibi.com.ph`, `pre-workteam@cibi.com.ph`, **and the requestor who raised the order** |
| **Subject** | unchanged: `CIBI \| Background Verification Information Request`, or the `Reminder:` variant |
| **Body** | unchanged — both already close by naming `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph` |

Neither body changed. The closing sentence already told the candidate to write to a CIBI mailbox, so a
visible `Cc` agrees with the copy they are reading rather than contradicting it.

This completes the set: **invitation**, **reminder**, **withdrawn**, **disputed** and **completed**
are now all copied to CIBI.

## Decisions

**Cc, not Bcc.** The candidate sees the team addresses. That is the intent — the body names a CIBI
mailbox in prose, so hiding the copy would be the odd choice, and `SubmittedFormEmail` already Cc's
the same two teams on a message the candidate is copied on. It is also the smaller change: the
sender's `SendATSEmailWithResultAsync` already takes a `cc` collection and carries it to
`MimeMessage.Cc`; a Bcc would need a new parameter threaded through four methods and its own
daily-cap arithmetic.

**Both the invitation and the reminder.** They share one send method, and the copy list does not
branch on which body is being sent. A team mailbox holds the chase next to the original, which is
what makes the thread readable. The cost is that a follow-up run copies the teams once per chased
order.

**Every send, including bulk.** No exemption for the queued path. See the cap note below.

**The requestor is resolved, not stored.** The order carries the requestor's *display name* — a
snapshot of `ICurrentUser.FullName` at order time — and a name is not an address. `RequestorId` is the
durable handle, and the Auth user directory is the only source for a mailbox, so the copy list looks
it up per send exactly as the withdrawn and completed-form notices already do. No column was added
and no DTO widened.

The requestor is **best-effort**: a null id, an id the directory no longer resolves, a resolved user
with no mailbox, and a directory that is unreachable all leave them off the copy and send anyway. The
candidate's link is the point of the message.

## The daily cap

Copied addresses are real recipients to the provider. `ATSEmailService.SendThroughAccountAsync`
charges `1 + cc.Count` to the sending account's daily cap and writes it to
`ats."EmailSendLog".RecipientCount`, which is **summed**, not counted.

So each entry in the copy list multiplies what a batch consumes. With two fixed teams plus the
requestor:

| Batch | Recipients charged before | After |
|---|---|---|
| 1 order | 1 | 4 |
| 500-row bulk upload | 500 | 2,000 |
| A follow-up run over 200 unanswered orders | 200 | 800 |

The "after" column is the ceiling, not a constant: an order whose requestor cannot be resolved costs
3 rather than 4.

Against a 450/day account limit, a 500-row upload already needed more than one account; it now needs
roughly four times the headroom. This was raised and accepted. The failover switcher handles a
capped account by moving to the next one, and an exhausted rotation returns `Throttled`, which defers
the row rather than retiring it — so the effect of under-provisioning is a slower queue, not lost
invitations. **Register enough sender accounts before the next large upload.**

## How it works

The send path already existed end to end; the teams are a new constant and the requestor is resolved
alongside them.

```text
single order:  InsertEmailInvitationRequestAsync (inside TransactionRunner)
                 -> SendApplicationFormToUserEmailAsync
resend:        ResendApplicationFormAsync requeues the row -> the queue below
bulk:          EmailNotificationProcessorService.SendEmailAsync
follow-up:     FollowUpEmailBackgroundJob releases rows -> the same processor

  all of them -> EndorsementSubmissionService.SendApplicationFormToUserEmailWithResultAsync
                   compose body (invitation or reminder)
                   BuildCopyListAsync(requestorId)
                     ApplicationFormEmail.CopyTeams        the fixed part
                     + SideEffectGuard( IAuthQueries.GetATSAssignedUserAsync )   the requestor, if resolvable
                   -> IAtsEmailSender.SendATSEmailWithResultAsync(..., cc: that list)
                      -> the pooled, capped, paced, failover-capable send
```

Every route funnels into one method, so the copy list was added in one place and applies to all of
them. There is no second send site to keep in step. `RequestorId` was already on
`EmailInvitationRequest` and on the DTO; it was simply threaded through both overloads of the send to
reach the lookup.

The directory read is wrapped in `SideEffectGuard` because the single-order path runs inside a
`TransactionRunner`: an unguarded throw there would roll back the whole order over a cosmetic copy.

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

The requestor degrades separately and more often. Four cases leave them off the `Cc` while the teams
and the candidate are unaffected, each covered by a unit test:

| Case | Why it happens |
|---|---|
| `RequestorId` is null | a bulk row or public API order raised without one; the directory is not consulted at all |
| the directory returns nothing | the user lost their ATS assignment since raising the order |
| the resolved user has no mailbox | blank or whitespace `UserEmail`; `MailboxAddress.Parse` would throw on it inside `BuildMessage` |
| the lookup throws | the directory is unreachable; `SideEffectGuard` logs and returns null |

Note the copy list is **not** deduplicated against the teams. A requestor whose own address is also a
team mailbox would be listed twice and charged twice. That cannot happen with the current lists —
the teams are shared mailboxes and requestors are individual logins — and the guard would need
revisiting if a team address ever became a real account.

## Manual verification

Unit tests stop at `IAtsEmailSender` — a successful SMTP send cannot be faked without a server. These
need a real pass:

1. Raise a single manual-screening order. Confirm the candidate's inbox shows both team addresses
   **and your own** in the `Cc` header, and that both team mailboxes received it.
2. Confirm the matching `ats."EmailSendLog"` row has `RecipientCount = 4`, not `1`.
3. Let a package follow-up fire, or release one, and confirm the reminder carries the same `Cc`,
   including the original requestor rather than whoever triggered the release.
4. Run a small bulk upload and confirm the per-account consumption in the email accounts screen moves
   by four per row.
5. Raise an order as a user with no ATS assignment, or with a blank email in the directory, and
   confirm the candidate still receives their link with the two teams copied and a warning logged.

## Before release

`ApplicationFormEmail.CopyTeams` currently holds a tester's mailboxes, with the real block commented
out directly above it. This matches the same swap already present in `SubmittedFormEmail`,
`WithdrawnEmail` and `DisputeEmail` on this branch.

**All four must be restored together.** The three sibling notice tests
(`SubmittedFormEmailNotificationTests`, `WithdrawnEmailNotificationTests`,
`DisputeEmailNotificationTests`) pin the real addresses as literals and are red until that happens —
they are doing exactly what they were written to do. They go green together when the constants are
restored.
