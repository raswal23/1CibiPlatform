# ATS Dispute Order Email

**Scope:** ATS disputes — the acknowledgement sent to whoever files a dispute from the console.
Touches the Blazor dialog, both request DTOs and the backend service; **no** new endpoint, route,
entity or migration.

## What it does

Filing a dispute from **Disputes → Send Dispute** already recorded the dispute and emailed an
internal CIBI operations mailbox. The person who filed it got nothing but a closing dialog. This
adds their acknowledgement.

| | |
|---|---|
| **To** | the person who filed the dispute, from their token |
| **Cc** | `clientsupport@cibi.com.ph` |
| **Subject** | `CIBI \| Order Dispute` |

The body is the standard CIBI card — navy gradient header, the same confidentiality footer as every
other ATS message — greeting the filer, naming the candidate the dispute is about, listing the
dispute category and (when there is one) the free-text details, then the closing sentence pointing at
`ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`.

**A dispute now produces two emails, on purpose.** The pre-existing one goes to
`ATS:DisputeOrderEmailRecipient` with subject `CIBI | Dispute Order Notification` and a table of
requestor email, company, order date and reason. It is an internal operations alert; this one is a
courtesy receipt for the filer. Different audiences, different subjects, different bodies, and —
importantly — different failure semantics, below.

## How it works

```text
DisputeDialogOrderComponent (Send Dispute -> confirm dialog)
  -> UI DisputeOrderService.MarkAsDisputedAsync
  -> PATCH ats/markasdisputed                  (gateway route already registered)
  -> MarkAsDisputedEndpoint -> MarkAsDisputedCommandHandler
  -> ATS DisputeOrderService.MarkAsDisputedAsync
       1. load order, check client assignment
       2. send the INTERNAL operations email      (pre-existing; before the write; throws)
       3. begin -> mark disputed -> order history -> save -> commit
       4. send the FILER acknowledgement          (new; after the commit; cannot throw)
```

Step 4 is the only new call. It goes to a new `IDisputeEmailNotification`, which resolves the two
body lines, asks `ATSEmailService` to compose them, and hands the result to the existing
`IAtsEmailSender` — so the acknowledgement travels the same pooled, capped, paced, failover-capable
path as every other ATS message.

`ats-dispute-order-email_code_explanation.md` walks the chain file by file.

## Why it is shaped this way

**The two emails fail differently, and that asymmetry is deliberate.** The operations notification
still runs *before* the transaction and still throws `InternalServerException` if it cannot be
delivered — so an SMTP outage stops disputes from being filed at all. That looks wrong next to the
guide's `SideEffectGuard` rule, and it is a real cost, but if operations never hear about a dispute
then nobody acts on it and the filing is worthless. The acknowledgement is the opposite case: the
dispute is already durable, the filer is not waiting on the message, and losing it degrades the
experience rather than the data — all three of the guard's conditions. It therefore runs after
`CommitAsync` and cannot throw, exactly like the withdrawal notice.

**One field feeds two lines.** The dialog offers three categories — Billing, Report, Others — and
only enables free text for Others. It has always collapsed both into a single `DisputeReason`:
the category label for Billing/Report, the typed text for Others. The template asks for a *category*
line and a *details* line, so the dialog now also sends `DisputeCategory` (always the selected
label) alongside the unchanged `DisputeReason`. Nothing is persisted differently:
`ATSRepository.DisputeOrders.cs:86` still writes `DisputeReason` into
`EmailInvitationRequest.DisputeCategory`, so Billing and Report rows keep storing the label and
Others rows keep storing the free text.

The notifier then renders the details bullet **only when the reason says something the category does
not** — a string comparison, not a check against the `"Others"` literal, which is a private const in
the Blazor component. A Billing dispute therefore shows one line, not `Category: Billing / Details:
Billing`. An empty details line was rejected for the same reason: it reads like a value failed to
load.

**The filer, not the order's requestor.** The acknowledgement goes to the address on the token,
reusing the value the service already reads for the operations email, so both messages name the same
person even on a token that only carries the short `email` claim. `ICurrentUser.FullName` supplies
the greeting, falling back to the address itself — `Dear ,` is worse than `Dear ana@client.com`.

**No link in the body.** Every other candidate-facing ATS body carries a button. This one has nothing
for the filer to do; a link would imply the dispute still needs something from them.

## How to verify

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Dispute"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AtsDisputeEmailBodyTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"   # adds Testcontainers; Docker must be running
dotnet build 1CibiPlatform.sln
```

Manual, end to end: in **Disputes**, file one dispute under **Billing** and one under **Others** with
text in "Please specify". For each, confirm the filer receives `CIBI | Order Dispute` with
`clientsupport@cibi.com.ph` on `Cc:`, that the Billing one shows only the category line while the
Others one shows both, and that the internal `CIBI | Dispute Order Notification` still arrives at
`ATS:DisputeOrderEmailRecipient`. Then check the row is `IsDisputed` with `DisputedAt` set and an
order-history `ReportDisputed` entry. Finally, point the sender at a dead account and confirm the
dispute is still recorded and the console still closes the dialog — the acknowledgement is lost and
logged, nothing more.

A successful SMTP send is not covered by unit tests and cannot be: `SmtpLease` wraps a real
`SmtpClient`. The tests stop at `IAtsEmailSender`, and the composed body is asserted directly since
that is a pure function. The `Cc:` header on the wire needs the manual pass above.

## What not to do

- **Do not move the acknowledgement before the commit**, and do not remove its `SideEffectGuard`.
  Either change turns an email outage into a 500 for a dispute that was filed successfully, and the
  filer will submit the same dispute again.
- **Do not "fix" the asymmetry by making the operations email best-effort too.** It is allowed to
  block the filing on purpose; see above.
- **Do not add a `DisputeDetails` column** to give the details line somewhere to live. The free text
  is already persisted, in `EmailInvitationRequest.DisputeCategory`, for Others rows. A new column
  was considered and rejected: it would need a migration, a repository change and a console column,
  to store a value that is already stored.
- **Do not change what `DisputeReason` means.** It is the persisted value and four existing tests
  assert the operations email receives it verbatim. `DisputeCategory` is the additive field.
- **Do not change the subject without the header.** The body's `<h1>` reads `DisputeEmail.Subject`,
  the same constant sent as the subject line.
- **Do not assume the body's contact sentence matches the CC list.** The sentence names both
  `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`; only the latter is actually copied. They are
  separate literals in separate files and nothing keeps them in step.
- **Do not confuse the two composers.** `ATSEmailService.BuildDisputeNotification` (this feature, on
  `IAtsEmailSender`) and `ATSEmailService.SendEmailForDispute` (the internal alert, on the shared
  `IEmailService`) are both about disputes and neither replaces the other.
