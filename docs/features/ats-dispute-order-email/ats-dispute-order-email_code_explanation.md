# ATS Dispute Order Email — Code Explanation

The call chain behind `ats-dispute-order-email.md`, traced hop by hop against the files as they now
stand. Read that document first for the *why*; this one is for changing the code.

## 1. The chain at a glance

| # | Hop | File | Member |
|---|---|---|---|
| 1 | Category radios + "Please specify" | `UI/FrontendWebassembly/Component/ATS/DisputeOrder/DisputeDialogOrderComponent.razor` | `MudRadioGroup` → `SelectedDisputeCategory`, `specifyReason` |
| 2 | Send Dispute | `…/DisputeDialogOrderComponent.razor.cs` | `SendDisputeAsync()` |
| 3 | UI HTTP service | `UI/FrontendWebassembly/Services/ATS/DisputeOrder/DisputeOrderService.cs:51` | `MarkAsDisputedAsync` → `PATCH ats/markasdisputed` (`:57`) |
| 4 | Gateway route | `BackendAPI/Modules/ATS/Path/ATSPaths.cs:559` | `RouteId: "MarkAsDisputed"` |
| 5 | Carter endpoint | `…/Features/Web/MarkAsDisputed/MarkAsDisputedEndpoint.cs:11` | `MapPatch("markasdisputed")` |
| 6 | Validator + handler | `…/MarkAsDisputedHandler.cs:7`, `:36` | `MarkAsDisputedCommandValidator`, `Handle` |
| 7 | Dispute write | `…/Services/DisputeOrder/DisputeOrderService.cs:94` | `MarkAsDisputedAsync` |
| 8 | **The acknowledgement** | `…/DisputeOrderService.cs:162` → `Services/EmailService/DisputeEmailNotification.cs:24` | `SendAsync` → `SendNoticeAsync:43` |
| 9 | Body | `…/Services/EmailService/ATSEmailService.cs:604` | `BuildDisputeNotification` |
| 10 | Send | `…/ATSEmailService.cs:61` | `SendATSEmailWithResultAsync` → `SendThroughAccountAsync:134` → `SendOverContextAsync:240` → `BuildMessage:386` |

Steps 1–7 existed before this feature; steps 1–3 gained one field. Steps 8–9 are the feature, and
step 10 is reused unchanged — its `cc` parameter came with the withdrawal notice
(`docs/features/ats-withdrawn-application-email/`).

There used to be a step between 7 and 8: an internal operations email composed by
`IEmailService.SendEmailForDispute` and sent to `ATS:DisputeOrderEmailRecipient` **before** the
transaction, throwing on failure. It has been removed, along with the interface member, its two other
implementations, the `SendDisputeOrderEmailAsync` helper, the `_disputeOrderEmailRecipient` field and
the config key in all five `appsettings` files. `DisputeOrderService` no longer depends on
`IEmailService` or `IConfiguration` at all.

## 2. The dialog sends two distinct values

`DisputeDialogOrderComponent.razor` offers three radios — `Billing`, `Report`, `Others` — each an
inline literal; the `OtherDisputeCategory` const that used to back the third one is gone, because
nothing singles it out any more.

The "Please specify" field (`id="dd-specify-reason"`) is always rendered, unconditionally
`Required="true"`, and gated only on having a category to describe:

```razor
	<div class="dd-input-wrap @(IsCategorySelected ? string.Empty : "is-locked")"
		 @onclick="OnSpecifyFieldClickedAsync">
		<MudTextField @bind-Value="specifyReason"
					  Disabled="@(!IsCategorySelected)"
					  Required="true"
					  RequiredError="Please specify a reason"
					  MaxLength="255"
```

`IsCategorySelected` is `!string.IsNullOrWhiteSpace(SelectedDisputeCategory)` — so the field unlocks
on the **first** selection and never re-locks. The `SelectedDisputeCategory` setter cancels the blink
instead of clearing the text: switching Billing to Report does not invalidate the sentence already
typed. `OnSpecifyFieldClickedAsync` returns immediately when `IsCategorySelected`, so the blink only
fires while genuinely locked.

What gets sent (`.razor.cs`, `SendDisputeAsync`):

```csharp
		var requestToSend = new DisputeOrderRequestDTO
		{
			EmailInvitationId = EmailInvitationId,
			// Every category now carries its own free text, so the two fields no longer collapse:
			// DisputeCategory is always the label and DisputeReason is always what the filer typed.
			DisputeReason = specifyReason.Trim(),
			DisputeCategory = SelectedDisputeCategory
		};
```

Ahead of that there is a belt-and-braces guard: while no category is selected the text field is
`Disabled`, and a disabled MudBlazor control is not guaranteed to carry its `Required` rule into
`MudForm`'s verdict. So `SendDisputeAsync` re-checks `specifyReason` itself, normalizes it to
`string.Empty`, re-runs `ValidateAsync()` to surface the error, and returns.

The UI serializes the whole DTO (`var request = new { disputeRequest };`), so no mapping code needed
touching.

## 3. Two DTOs that must agree by name alone

`BackendAPI/Modules/ATS/DTO/DisputeOrderRequestDTO.cs` and
`UI/FrontendWebassembly/DTO/ATS/DisputeOrderRequestDTO.cs` both gained:

```csharp
	public string? DisputeCategory { get; set; }
```

They are bound by JSON property name. Nothing checks that they match, and a rename on one side
silently delivers `null` to the other — which degrades to the fallback in §5 and §8 rather than
failing loudly. Their XML docs now state which is which: `DisputeReason` is the filer's free text and
is **not** persisted; `DisputeCategory` is the label and **is**.

The validator (`MarkAsDisputedHandler.cs:7`) requires `EmailInvitationId` and a non-empty
`DisputeReason` under 255 characters — now required for every category, since the console asks all
three to describe themselves. `DisputeCategory` has a `MaximumLength(255)` rule matching the column
it is written into, but deliberately **no** `NotEmpty`: a client that predates the split sends the
label in `DisputeReason` instead, and the repository falls back to it (§8). Such a client still files
a dispute and still gets an acknowledgement.

## 4. The service: one send, after the commit

`DisputeOrderService.MarkAsDisputedAsync` (`:94`). Before the transaction it gathers what the notice
needs — `requestor` from the token at `:121` (`ClaimTypes.Email`, falling back to the short `"email"`
claim), and `subjectName` from the order's name parts at `:123`.

Then the transaction, and after `CommitAsync` (`:142`):

```csharp
			var candidateName = string.IsNullOrWhiteSpace(subjectName)
				? order.EmailAddress ?? "this order"
				: subjectName;

			await _disputeEmailNotification.SendAsync(
				new DisputeEmailDetails(
					order.EmailInvitationID,
					requestor,
					_currentUser.FullName,
					candidateName,
					disputeRequest.DisputeCategory,
					disputeRequest.DisputeReason!),
				cancellationToken);
```

Three things to notice:

- It is **inside** the `try` whose `catch` rolls back and rethrows. Safe only because `SendAsync`
  cannot throw (§5). Remove the guard and a delivery failure would roll back a committed dispute and
  then surface as a 500.
- `order.EmailInvitationID` travels with the details even though the email body never uses it — the
  acknowledgement records itself against that order's history (§8 of the high-level document), and
  the timeline is per order.
- `DisputeReason!` — the null-forgiving is safe because `MarkAsDisputedCommandValidator` has already
  rejected an empty reason. It is the only place in this method that leans on the validator.

`candidateName` falls back to the order's email address because the copy reads "A dispute has been
submitted for \<candidate\>", and an order with no name parts still has to identify someone.

The method no longer injects `IEmailService` or `IConfiguration`; both existed solely for the removed
operations alert.

## 5. `DisputeEmailNotification`

Two files under `Services/EmailService/`, following the module's one-type-per-file convention:

- `IDisputeEmailNotification.cs` — the contract, plus the `DisputeEmailDetails` record (`:46`).
  Values rather than the order entity, because the filer comes from the token and the candidate from
  the row; no single object carries both.
- `DisputeEmailNotification.cs` — the implementation.

```csharp
	public async Task SendAsync(
		DisputeEmailDetails details,
		CancellationToken cancellationToken)
	{
		// Guarded here rather than at the call site, matching WithdrawnEmailNotification and
		// AtsNotificationService. ...
		await SideEffectGuard.RunAsync(
			() => SendNoticeAsync(details, cancellationToken),
			_logger,
			$"acknowledge the dispute filed by {details.RequestorEmail}",
			cancellationToken);
	}
```

`SendNoticeAsync` (`:41`) throws freely *because* its only caller is that guard. In order:

1. **Bail if there is no address.** `RequestorEmail` comes from a claim and is absent on tokens
   issued before it existed. Logged and skipped — the dispute is already recorded.
2. **Derive the two lines.**

```csharp
		var category = string.IsNullOrWhiteSpace(details.DisputeCategory)
			? details.DisputeReason
			: details.DisputeCategory;

		var hasSeparateDetails =
			!string.IsNullOrWhiteSpace(details.DisputeReason)
			&& !string.Equals(details.DisputeReason, category, StringComparison.Ordinal);
```

   Comparing the two values rather than testing for `"Others"` is the point: this service has no
   business knowing which categories exist. Since every category now carries its own free text, the
   usual path renders **both** lines. Two cases still collapse to one: a client that predates the
   split and sent only the label in `DisputeReason`, and a filer who typed the category's own name
   into "Please specify". Both would otherwise read `Category: Report / Details: Report`.
3. **Greeting name.** `RequestorName` (the token's `FullName`), falling back to the address.
4. **Compose** via `_emailSender.BuildDisputeNotification(...)`, passing `null` for the details when
   `hasSeparateDetails` is false.
5. **Send** with `cc: [DisputeEmail.CopyTeam]` and `subject: DisputeEmail.Subject`, then log. A
   `!result.IsSent` is a warning, not an exception — `Throttled` means defer, and there is no queue
   behind an acknowledgement.

## 6. The composer and the constants

`ATSEmailService.BuildDisputeNotification` (`:604`), declared on `IAtsEmailSender` — not on the
shared `IEmailService`, for the reason that interface's own remarks give: Auth and the test fakes
implement it and have no dispute to acknowledge.

It reuses the card markup from the invitation, reminder and withdrawal notices, and its header reads
the shared constant so subject and header cannot drift:

```csharp
						<h1 style='margin:0;font-size:20px'>{DisputeEmail.Subject}</h1>
```

Every interpolated value is HTML-encoded — the filer's name from a claim, the candidate's from the
row, and the dispute text typed into the console, which is the one an authenticated user controls
directly.

The details bullet is built outside the verbatim string and dropped in:

```csharp
		var detailsBullet = string.IsNullOrWhiteSpace(disputeDetails)
			? string.Empty
			: $"<li style='margin:6px 0;font-size:15px;line-height:1.6'><span style='color:#5b6f8f'>Dispute Details:</span> {WebUtility.HtmlEncode(disputeDetails.Trim())}</li>";
```

`Constants/DisputeEmail.cs` holds `Subject` (`:22`) and `CopyTeam`. Read by the notifier and by the
composer, so one change moves both.

## 7. Wiring that is not visible from any one file

| Thing | Where it must agree | Enforced by |
|---|---|---|
| Subject vs. body header | `Constants/DisputeEmail.cs` `Subject`, read by `DisputeEmailNotification.SendNoticeAsync` and by `BuildDisputeNotification` | The shared constant only |
| CC address vs. body copy | `DisputeEmail.CopyTeam` = `clientsupport@cibi.com.ph`, but the closing sentence names `ccteam@cibi.com.ph` **and** `clientsupport@cibi.com.ph` as prose | **Nothing** — separate literals in separate files |
| `DisputeCategory` | `UI/…/DTO/ATS/DisputeOrderRequestDTO.cs` vs. `BackendAPI/Modules/ATS/DTO/DisputeOrderRequestDTO.cs` | JSON property name only |
| Which field reaches the column | The dialog sends both; `ATSRepository.DisputeOrders.cs:87` writes `DisputeCategory` into `EmailInvitationRequest.DisputeCategory` and falls back to `DisputeReason` | Nothing — see §8 |
| Route `/ats/markasdisputed` | `ATSPaths.cs:559` `MatchPath` vs. the UI's `PatchAsJsonAsync` vs. Carter's `MapPatch` + `PathSet` | Nothing at compile time |
| `IDisputeEmailNotification` | `ATSServiceConfiguration.cs:162`, registered beside the sender it depends on | DI |
| `IAtsEmailSender` in tests | The ATS integration host must register a fake implementing it — `FakeAtsEmailSender`, which gained `BuildDisputeNotification` | Nothing; see the withdrawal feature's `_code_explanation.md` §7.1 |

## 8. Only the label is persisted

`ATSRepository.DisputeOrders.cs:81`:

```csharp
		var category = string.IsNullOrWhiteSpace(disputeRequest.DisputeCategory)
			? disputeRequest.DisputeReason
			: disputeRequest.DisputeCategory;

		var affectedRows = await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == disputeRequest.EmailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.DisputeCategory, category)
				.SetProperty(eir => eir.DisputedAt, DateTime.UtcNow),
				cancellationToken);
```

The column receives the **category label**, and the fallback to `DisputeReason` covers a client that
predates the split.

This line used to write `disputeRequest.DisputeReason` unconditionally. That worked while the dialog
collapsed both values into one field — for Billing and Report the reason *was* the label, and only an
Others row held free text. Once "Please specify" became required for all three, writing the reason
there would have put a sentence in every row. The disputes grid projects that column
(`ATSRepository.DisputeOrders.cs:39` → `DisputeOrderListDTO.DisputeCategory`) into the console's
"Reason for Dispute" chip, so every chip would have become a sentence. Writing the label keeps the
chip reading `Billing` / `Report` / `Others` exactly as it did before.

**No migration, and no new column.** The free text is not persisted at all — it exists to fill the
acknowledgement's details line and is composed from the request in the same request. A `DisputeDetails`
column was considered and rejected; see the high-level document's "What not to do".

## 9. Tests

`Test/Test/BackendAPI/Modules/ATS.UnitTests/`:

| File | Covers |
|---|---|
| `DisputeEmailNotificationTests.cs` | Filer addressed and `clientsupport@cibi.com.ph` copied; both lines rendered for **every** category (a `[Theory]` over Billing/Report/Others) and the details line **omitted** when the reason equals the category; reason used as the category when none was sent; greeting falls back to the address; skipped when there is no address; **does not throw** when the sender throws or reports failure |
| `AtsDisputeEmailBodyTests.cs` | The composed body: exact copy, both contact addresses, header equals `DisputeEmail.Subject`, both bullets present with details and the details line absent without, values HTML-encoded, and every `href` is a `mailto:` |
| `DisputeOrderServiceTests.cs` — `#region Requestor Acknowledgement` | Orchestration only: acknowledgement sent after the commit with the right values including the order id; category and reason passed through unmangled (`..._ShouldPassCategoryAndReasonThroughUnmangled`); candidate falls back to the order's address; **not** sent when the write fails |

`DisputeOrderServiceIntegrationTests` pins the persistence rule of §8 from both directions: the
normal path asserts the **label** lands in `EmailInvitationRequest.DisputeCategory` rather than the
typed sentence, and `MarkAsDisputedAsync_ShouldPersistTheReason_WhenTheClientSendsNoCategory` sends
only `DisputeReason` and asserts the fallback still fills the column. Without the second test the
fallback branch would be reachable only from a client nobody runs.

The subject and the CC address are asserted as **literals**, not by reading `DisputeEmail`, so a
change to the agreed copy fails a test rather than silently redefining it.

Removing the operations alert changed four pre-existing tests rather than simply deleting them.
`MarkAsDisputedAsync_ShouldSendNotificationUpdateRepositoryAndReturnTrue` became
`..._ShouldMarkTheOrderDisputedAndReturnTrue` and now asserts the write and the commit.
`..._ShouldUseFallbackEmailClaim_WhenStandardEmailClaimIsMissing` was repurposed rather than dropped —
the short-`email`-claim fallback still matters, it now has to reach the acknowledgement instead of
the operations mailbox, so it asserts the notifier receives the fallback address.
`..._ShouldThrowAndSkipRepository_WhenEmailReturnsFalse` was deleted outright: the behaviour it
pinned no longer exists. `..._ShouldWrapRepositoryFailure_AfterEmailIsSent` became
`..._ShouldWrapRepositoryFailure_AndNotAcknowledgeTheFiler`.

`DisputeOrderServiceIntegrationTests` stubs `IDisputeEmailNotification` and its `CreateService`
factory no longer takes an `IEmailService`, so it keeps exercising the dispute write and the cache
invalidation. Its `MarkAsDisputedAsync_ShouldThrowAndPreserveOrder_WhenEmailCannotBeSent` was deleted
for the same reason as the unit test above. **That leaves a coverage gap worth naming:** no
integration test now proves the dispute is recorded when delivery fails, because the swallow lives
inside the real notifier and a mocked one cannot exercise it.
`DisputeEmailNotificationTests.SendAsync_ShouldNotThrow_WhenTheSenderThrows` covers the guarantee at
unit level instead.

Not covered, and not coverable by unit tests: a successful SMTP send, so the `Cc:` header on the wire
needs the manual pass in the high-level document.

## 10. Change X, also check Y

| If you change… | Also check… |
|---|---|
| `DisputeEmail.Subject` | The header follows automatically; the **tests** do not — they pin the literal |
| `DisputeEmail.CopyTeam` | The closing sentence in `BuildDisputeNotification`, which names it as prose alongside `ccteam@cibi.com.ph` (§7) |
| The body copy | `AtsDisputeEmailBodyTests` asserts the sentences verbatim |
| The dialog's categories, or which ones require free text | `DisputeEmailNotification.SendNoticeAsync` step 2 — it compares values rather than matching `"Others"`, so it should keep working, but `DisputeEmailNotificationTests` pins all three categories by name |
| `DisputeOrderRequestDTO` on either side | The other side (§3), and `MarkAsDisputedCommandValidator` — note `DisputeCategory` is deliberately not `NotEmpty` |
| Which field is persisted | `ATSRepository.DisputeOrders.cs:87`, the disputes grid's chip (`:39`), the details-line derivation in `DisputeEmailNotification.SendNoticeAsync` step 2, both DTOs' XML docs, and §8. The integration suite pins both directions |
| `IAtsEmailSender`'s members | `ATSEmailService`, `FakeAtsEmailSender` (the integration host — §7), and the mocks in `AtsEmailAccountManagementFixture.cs:34` |
| `DisputeOrderService`'s constructor | `DisputeOrderServiceTests.cs` and `DisputeOrderServiceIntegrationTests.cs` both news it up by hand |
| The transaction in `MarkAsDisputedAsync` | The acknowledgement must stay after `CommitAsync`, and `SendAsync` must stay unable to throw (§5) |
| `IEmailService`'s members | It is a BuildingBlocks contract implemented by `ATSEmailService`, `BuildingBlocks/…/EmailService.cs` and the tests' `FakeEmailSender`. `SendEmailForDispute` was removed from all four; do not put a dispute composer back on it (§10 of the high-level document) |
| `ATS:DisputeOrderEmailRecipient` | Nothing reads it any more. It was deleted from all five `appsettings` files — if an environment still exports `ATS__DISPUTEORDEREMAILRECIPIENT`, that variable is now inert |
