# ATS Withdrawn Application Email

**Scope:** ATS application form — the withdrawal notice sent when a candidate withdraws their own
form. Backend only; no new endpoint, route, DTO, entity, migration or UI change.

## What it does

A candidate who opens their emailed application-form link can withdraw it. Confirming the
"Withdraw Application" dialog already moved the order to `ApplicationFormStatus = Withdrawn` and
`OrderStatus = Application Withdrawn` and wrote an order-history row — and then told nobody. The
requestor who raised the order found out only by looking at the console.

This adds the missing notice. Once the withdrawal has committed, the requestor receives:

| | |
|---|---|
| **To** | the requestor's mailbox, resolved from the order's `RequestorId` |
| **Cc** | `ccteam@cibi.com.ph`, then the candidate's own address |
| **Subject** | `Order Status – Withdrawn` |

The body is the standard CIBI card — navy gradient header, the same confidentiality footer as every
other ATS message — carrying three sentences: the greeting, the fact that the candidate withdrew and
that the verification should not proceed without the completed form, and the two contact addresses
(`ccteam@cibi.com.ph`, `clientsupport@cibi.com.ph`).

It is addressed to the requestor rather than the candidate on purpose. The candidate is the one who
pressed the button; copying them puts the notice on record for them without telling them something
they just did. The requestor is the only person who can act on it, because the point of the message
is operational: **stop the verification.**

## How it works

The whole flow already existed up to the commit:

```text
Blazor Withdraw button -> CancelTransaction -> confirm dialog
  -> UI ApplicationFormService.WithdrawApplicationForm
  -> PATCH ats/withdrawnapplicationform          (gateway route already registered)
  -> WithdrawnApplicationFormEndpoint -> WithdrawnApplicationFormHandler
  -> ATS ApplicationFormService.WithdrawnApplicationForm
       begin -> update status -> record order history -> save -> commit
```

One call was added after that commit, to a new `IWithdrawnEmailNotification`. That service resolves
the requestor, asks `ATSEmailService` to compose the body, and hands it to the existing
`IAtsEmailSender` — so the notice travels the same pooled, capped, paced, failover-capable path as
every other ATS message rather than opening its own SMTP session.

`docs/features/ats-withdrawn-application-email/ats-withdrawn-application-email_code_explanation.md`
walks the chain file by file.

## Order history

The notice writes its own row, `WithdrawalNoticeEmail`, beside the `ApplicationFormWithdrawn` row the
withdrawal itself already wrote. Two rows for one action is deliberate: "the subject withdrew" and
"we told the requestor" are different facts, and the second can fail while the first already
happened.

It records the **attempt**, so the row is written whether or not the email was delivered — a failed
send still appears on the timeline and the reason is in the log. It is *not* written when no send was
attempted at all (no requestor id, or the requestor is no longer in the ATS directory). The status
pair is `null → Application Withdrawn`; the previous side is null because nothing moved.

It renders in `OrderStatusHistoryDialog` as "Withdrawal notice", neutral tone, send icon. Detail in
`docs/features/ats-order-status-history/`.

## Why it is shaped this way

**After the commit, and it cannot throw.** The withdrawal is the thing that matters. If the notice
failed and the exception reached `CustomExceptionHandler`, the candidate would get a 500 for a
withdrawal that had already landed — and would read it as "my withdrawal failed". The notifier
therefore wraps its own work in `SideEffectGuard`, the same contract `AtsNotificationService`
already keeps, and its callers just await it. A dead SMTP account costs one log line.

This is also why the send is not merely *placed* after the commit but *provably* after it: a test
makes the commit throw and asserts no notice went out. Announcing a withdrawal that did not land
would tell a requestor to stop a verification that is still running.

**The requestor's address is not on the order.** `EmailInvitationRequest.Requestor` holds a display
name and `RequestorId` an Auth user id. The mailbox comes from
`IAuthQueries.GetATSAssignedUserAsync` — the same lookup the OMS ticketing processor makes, cached
behind `AuthCacheRepository`. An order with no `RequestorId` (the public API raises orders with none)
or a requestor who has since lost their ATS assignment is logged and skipped, not sent to a
substitute address: a withdrawal notice delivered to the wrong mailbox is worse than one not
delivered.

**No link in the body.** Every other candidate-facing ATS body carries an "Application Form" button.
This one must not — the form is gone, and a button would invite the requestor to reopen something
the candidate just closed.

**CC was added to the shared sender, not worked around.** `BuildMessage` set only `From`, `To` and
`Subject`. The alternative — two separate messages — would have produced no `Cc:` header at all and
charged the daily cap twice. Instead `SendATSEmailWithResultAsync` and `SendThroughAccountAsync`
gained an optional trailing `cc`, and `SendThroughAccountAsync` now reports `1 + copied.Count`
recipients to the quota log instead of a literal `1`. `AtsEmailSendLog.RecipientCount` already
summed recipients rather than counting rows, and both that entity and
`AtsEmailAccountRepository.BuildSnapshotQuery` carried comments anticipating exactly this change —
so the schema and the cap accounting did not have to move. Optional-and-last is what kept the two
existing production callers and four existing tests compiling untouched.

## How to verify

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~WithdrawnEmail"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AtsWithdrawnEmailBodyTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"   # adds Testcontainers; Docker must be running
dotnet build 1CibiPlatform.sln
```

Manual, end to end: open an invitation link, click **Withdraw**, confirm. The screen should show the
withdrawn state. Then check that the requestor received the notice with the candidate and
`ccteam@cibi.com.ph` on `Cc:` and the exact subject, that the row reads
`ApplicationFormStatus = Withdrawn` / `OrderStatus = Application Withdrawn`, and that an
`ApplicationFormWithdrawn` order-history row exists. Finally, point the sender at a dead account and
confirm the candidate still sees a successful withdrawal while the failure appears in the log.

A successful SMTP send is not covered by unit tests and cannot be: `SmtpLease` wraps a real
`SmtpClient`. The tests stop at `IAtsEmailSender`, and the composed body is asserted directly since
that is a pure function. The `Cc:` header itself and the 3-recipient quota charge need the manual
pass above.

The full `~ATS` run is worth doing even though this feature adds no integration test of its own: it
resolves the real container, which is how the `IAtsEmailSender` registration constraint below was
found.

**Flaky, and not yours:** `ResendApplicationFormIntegrationTests` fails intermittently with "Failed
to find email invitation for resend". While this feature was in progress it failed in three of four
full runs — once at pristine `HEAD` with every change stashed, and once when that class was run on
its own — then passed twice consecutively with no behavioural change in between. It is unrelated to
the withdrawal notice. Re-run before drawing a conclusion from it, and do not try to fix it as part
of an email change.

## What not to do

- **Do not move the send before the commit**, and do not remove the `SideEffectGuard`. Either change
  turns an email outage into a candidate-facing failure for a withdrawal that succeeded.
- **Do not put the composer on `IEmailService`.** That interface is a BuildingBlocks contract
  implemented by Auth and by the tests' `FakeEmailSender`, neither of which has a withdrawal to
  announce. ATS-only composers go on `IAtsEmailSender`; the interface's own remarks say why.
- **Do not inject `IAtsEmailSender` into a new ATS service without checking the integration host.**
  It is registered by *casting* the keyed `"ats"` `IEmailService`, so the ATS test host must register
  a fake that implements it — `FakeAtsEmailSender`. Get this wrong and the container throws while
  resolving, taking down every integration test that reaches the service, including ones about
  persistence that have nothing to do with email. `_code_explanation.md` §7.1 has the detail.
- **Do not change the subject without the header.** The body's `<h1>` reads
  `WithdrawnEmail.Subject` — the same constant sent as the subject line. They are read together in a
  mail client preview, and a mismatch looks like a mis-send. Nothing enforces this at compile time
  beyond the shared constant.
- **Do not address the candidate.** If the requirement ever changes, remember that the candidate's
  address can be absent and already degrades to being left off the copy.
- **Do not hardcode a recipient count.** If a third address is ever copied, the quota figure follows
  automatically from `1 + copied.Count`; a literal would silently under-report consumption and
  retire accounts late.
- **A withdrawn order is never chased.** `ReleaseDueFollowUpInvitationsAsync` filters
  `ApplicationFormStatus = Pending`, so the follow-up reminder cannot fire after a withdrawal. Do
  not widen that predicate without re-reading this document.
