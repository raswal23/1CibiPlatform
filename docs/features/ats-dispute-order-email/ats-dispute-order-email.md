# ATS Dispute Order Email

**Scope:** ATS disputes — the acknowledgement sent to whoever files a dispute from the console.
Touches the Blazor dialog, both request DTOs, the backend service and the shared `IEmailService`
contract; **no** new endpoint, route, entity or migration.

## What it does

Filing a dispute from **Disputes → Send Dispute** records the dispute and emails the person who filed
it, confirming receipt and restating what they submitted.

| | |
|---|---|
| **To** | the person who filed the dispute, from their token |
| **Cc** | `clientsupport@cibi.com.ph` |
| **Subject** | `CIBI \| Order Dispute` |

The body is the standard CIBI card — navy gradient header, the same confidentiality footer as every
other ATS message — greeting the filer, naming the candidate the dispute is about, listing the
dispute category and (when there is one) the free-text details, then the closing sentence pointing at
`ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`.

**This is the only email a dispute produces.** There used to be a second one: an internal operations
alert to the `ATS:DisputeOrderEmailRecipient` mailbox, subject `CIBI | Dispute Order Notification`,
carrying a table of requestor email, company, order date and reason. It has been removed, along with
`IEmailService.SendEmailForDispute` (all three implementations), the `SendDisputeOrderEmailAsync`
helper and the config key from all five `appsettings` files. CIBI now learns of a dispute from the
`clientsupport@cibi.com.ph` copy on this message and from the order's history, not from a separate
message.

## How it works

```text
DisputeDialogOrderComponent (Send Dispute -> confirm dialog)
  -> UI DisputeOrderService.MarkAsDisputedAsync
  -> PATCH ats/markasdisputed                  (gateway route already registered)
  -> MarkAsDisputedEndpoint -> MarkAsDisputedCommandHandler
  -> ATS DisputeOrderService.MarkAsDisputedAsync
       1. load order, check client assignment
       2. begin -> mark disputed -> order history -> save -> commit
       3. send the filer acknowledgement          (after the commit; cannot throw)
```

Step 3 goes to `IDisputeEmailNotification`, which resolves the two body lines, asks
`ATSEmailService` to compose them, and hands the result to the existing `IAtsEmailSender` — so the
acknowledgement travels the same pooled, capped, paced, failover-capable path as every other ATS
message.

`ats-dispute-order-email_code_explanation.md` walks the chain file by file.

## Order history

The acknowledgement writes its own row, `DisputeAcknowledgementEmail`, beside the `ReportDisputed` row
the filing itself already wrote. Two rows for one action is deliberate: "the report was disputed" and
"we acknowledged it to the filer" are different facts, and the second can fail while the first
already happened.

It records the **attempt**, so the row is written whether or not the email was delivered — a failed
send still appears on the timeline and the reason is in the log. It is *not* written when no send was
attempted at all (the filer's token carries no email claim). The status pair is `null → Completed`;
the previous side is null because a dispute does not move the order, and `Completed` matches the
fallback the `ReportDisputed` row beside it uses.

It renders in `OrderStatusHistoryDialog` as "Dispute acknowledgement", neutral tone. Detail in
`docs/features/ats-order-status-history/`.

## Why it is shaped this way

**After the commit, and it cannot throw.** The dispute is durable by the time the acknowledgement is
attempted, the filer is not waiting on the message, and losing it degrades the experience rather than
the data — all three of the guide's `SideEffectGuard` conditions. So it runs after `CommitAsync` and
swallows its own failures, exactly like the withdrawal and completion notices.

That is a behaviour change from the operations alert it replaced, which ran *before* the transaction
and threw `InternalServerException` on failure — so an SMTP outage stopped disputes from being filed
at all. Removing it decouples the two: a dispute is now always recorded, and a dead mailbox costs one
log line. The trade is that CIBI no longer gets a delivery-guaranteed alert; it gets a copy on a
best-effort message plus the `ReportDisputed` history row, which is written inside the transaction
and so cannot be lost.

**Two fields, two lines.** The dialog offers three categories — Billing, Report, Others — and
**all three require the "Please specify" description**. That field used to be enabled for Others
alone; a Billing or Report dispute arrived as a bare label, which told whoever picked it up nothing.
So the request now carries both values distinctly: `DisputeCategory` is always the selected label and
`DisputeReason` is always what the filer typed. The template asks for a *category* line and a
*details* line, and it now gets one of each on every dispute.

The field stays **disabled until a category is picked** — clicking it while locked blinks the note
underneath rather than swallowing the click. It does not re-lock or clear once a category is chosen:
switching Billing to Report does not make the sentence already typed wrong.

Only the **label** is persisted. `ATSRepository.DisputeOrders.cs` writes `DisputeCategory` into
`EmailInvitationRequest.DisputeCategory`, which is the column the disputes grid projects into its
"Reason for Dispute" chip — so the chip keeps reading `Billing` / `Report` / `Others` rather than a
sentence. The free text is not stored; it exists to fill the acknowledgement's details line. The
repository falls back to `DisputeReason` when no category arrives, which is what a client predating
the split sends.

The notifier renders the details bullet **only when the reason says something the category does
not** — a string comparison, not a check against the `"Others"` literal, which is a private const in
the Blazor component. With every category carrying its own text the normal path is now both lines;
the collapse survives for the two odd cases, a legacy client and a filer who types the category's own
name into the description. Either would otherwise read `Category: Billing / Details: Billing`. An
empty details line was rejected for the same reason: it reads like a value failed to load.

**The filer, not the order's requestor.** The acknowledgement goes to the address on the token, read
with a fallback from `ClaimTypes.Email` to the short `email` claim so it still reaches the person who
pressed the button on older tokens. `ICurrentUser.FullName` supplies the greeting, falling back to
the address itself — `Dear ,` is worse than `Dear ana@client.com`.

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

Manual, end to end: in **Disputes**, open the dialog and confirm "Please specify" is locked until a
category is selected and that clicking it while locked blinks the note. Then file one dispute under
**Billing** and one under **Others**, each with its own text in "Please specify". For each, confirm
the filer receives `CIBI | Order Dispute` with `clientsupport@cibi.com.ph` on `Cc:`, that **both**
the category and the details line are present, that the grid's "Reason for Dispute" chip shows the
label rather than the typed sentence, and that **no second message arrives** — the internal
operations alert is gone. Then check the row is `IsDisputed` with `DisputedAt` set, and that the order's history shows
both a `ReportDisputed` and a `DisputeAcknowledgementEmail` entry. Finally, point the sender at a
dead account and confirm the dispute is still recorded, the console still closes the dialog, and the
`DisputeAcknowledgementEmail` row *still appears* — it records the attempt, so a failed send leaves a
row and a log line rather than silence.

A successful SMTP send is not covered by unit tests and cannot be: `SmtpLease` wraps a real
`SmtpClient`. The tests stop at `IAtsEmailSender`, and the composed body is asserted directly since
that is a pure function. The `Cc:` header on the wire needs the manual pass above.

## What not to do

- **Do not move the acknowledgement before the commit**, and do not remove its `SideEffectGuard`.
  Either change turns an email outage into a 500 for a dispute that was filed successfully, and the
  filer will submit the same dispute again.
- **Do not reintroduce a send that blocks the filing.** The removed operations alert threw when it
  could not be delivered, which meant an SMTP outage stopped disputes from being recorded at all. If
  operations needs a guaranteed alert again, build it off the `ReportDisputed` history row or a
  queued worker — not off the request that writes the dispute.
- **Do not add a `DisputeDetails` column** to give the free text somewhere to live. It was considered
  and rejected when "Please specify" became required for every category: it needs a migration, a
  repository change, a DTO field and a console column, to store a value nothing reads back. The
  acknowledgement email is the only consumer, and it is composed from the request in the same
  request. If a future screen genuinely needs to *display* the description, that is the point to add
  the column — not before.
- **Do not persist `DisputeReason` into `EmailInvitationRequest.DisputeCategory`.** That column feeds
  the disputes grid's "Reason for Dispute" chip. It used to receive `DisputeReason`, which was the
  label for Billing/Report and free text for Others; now that all three carry free text, writing the
  reason there would turn every chip into a sentence. The repository writes `DisputeCategory` and
  falls back to `DisputeReason` only when no category arrives.
- **Do not change the subject without the header.** The body's `<h1>` reads `DisputeEmail.Subject`,
  the same constant sent as the subject line.
- **Do not assume the body's contact sentence matches the CC list.** The sentence names both
  `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`; only the latter is actually copied. They are
  separate literals in separate files and nothing keeps them in step.
- **Do not put a dispute composer back on `IEmailService`.** `SendEmailForDispute` was removed from
  that interface precisely because it is a BuildingBlocks contract implemented by Auth and by the
  tests' `FakeEmailSender`, neither of which files disputes. `BuildDisputeNotification` lives on
  `IAtsEmailSender` for that reason; a new dispute body belongs there too.
