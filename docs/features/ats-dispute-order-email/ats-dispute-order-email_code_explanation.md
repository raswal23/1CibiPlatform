# ATS Dispute Order Email — Code Explanation

The call chain behind `ats-dispute-order-email.md`, traced hop by hop against the files as they now
stand. Read that document first for the *why*; this one is for changing the code.

## 1. The chain at a glance

| # | Hop | File | Member |
|---|---|---|---|
| 1 | Category radios + "Please specify" | `UI/FrontendWebassembly/Component/ATS/DisputeOrder/DisputeDialogOrderComponent.razor:44`, `:66` | `MudRadioGroup` → `SelectedDisputeCategory` |
| 2 | Send Dispute | `…/DisputeDialogOrderComponent.razor.cs:101` | `SendDisputeAsync()`, request built at `:115` |
| 3 | UI HTTP service | `UI/FrontendWebassembly/Services/ATS/DisputeOrder/DisputeOrderService.cs:51` | `MarkAsDisputedAsync` → `PATCH ats/markasdisputed` (`:57`) |
| 4 | Gateway route | `BackendAPI/Modules/ATS/Path/ATSPaths.cs:559` | `RouteId: "MarkAsDisputed"` |
| 5 | Carter endpoint | `…/Features/Web/MarkAsDisputed/MarkAsDisputedEndpoint.cs:11` | `MapPatch("markasdisputed")` |
| 6 | Validator + handler | `…/MarkAsDisputedHandler.cs:7`, `:36` | `MarkAsDisputedCommandValidator`, `Handle` |
| 7 | Dispute write | `…/Services/DisputeOrder/DisputeOrderService.cs:102` | `MarkAsDisputedAsync` |
| 7a | *Internal* ops email (pre-existing) | `…/DisputeOrderService.cs:139` → `:212` | `SendDisputeOrderEmailAsync` |
| 8 | **The acknowledgement** | `…/DisputeOrderService.cs:188` → `Services/EmailService/DisputeEmailNotification.cs:22` | `SendAsync` → `SendNoticeAsync:41` |
| 9 | Body | `…/Services/EmailService/ATSEmailService.cs:604` | `BuildDisputeNotification` |
| 10 | Send | `…/ATSEmailService.cs:61` | `SendATSEmailWithResultAsync` → `SendThroughAccountAsync:134` → `SendOverContextAsync:240` → `BuildMessage:386` |

Everything except step 8 existed before. Steps 1–3 gained one field; 9–10 gained one composer. The
`cc` parameter at step 10 was added by the withdrawal notice
(`docs/features/ats-withdrawn-application-email/`) and is reused unchanged here.

## 2. The dialog collapses two values into one

`DisputeDialogOrderComponent.razor:44` offers three radios — `Billing`, `Report`, and
`OtherDisputeCategory` (`"Others"`, a private const at `.razor.cs:5`). The "Please specify" field at
`:66` is always rendered but `Disabled="@(!IsOtherDisputeSelected)"`, so free text exists only for
Others.

What gets sent (`.razor.cs:115`):

```csharp
		var requestToSend = new DisputeOrderRequestDTO
		{
			EmailInvitationId = EmailInvitationId,

			// DisputeReason keeps the meaning it has always had - it is what gets persisted, and
			// for Billing/Report that has always been the category label rather than free text.
			// DisputeCategory travels alongside it only so the acknowledgement email can show the
			// category and the "Others" free text as two separate lines.
			DisputeReason = IsOtherDisputeSelected
				? otherReason.Trim()
				: SelectedDisputeCategory,
			DisputeCategory = SelectedDisputeCategory
		};
```

`DisputeReason` is unchanged — same expression, same value, same meaning. `DisputeCategory` is purely
additive. The UI serializes the whole DTO (`var request = new { disputeRequest };`), so no
mapping code needed touching.

## 3. Two DTOs that must agree by name alone

`BackendAPI/Modules/ATS/DTO/DisputeOrderRequestDTO.cs` and
`UI/FrontendWebassembly/DTO/ATS/DisputeOrderRequestDTO.cs` both gained:

```csharp
	public string? DisputeCategory { get; set; }
```

They are bound by JSON property name. Nothing checks that they match, and a rename on one side
silently delivers `null` to the other — which degrades to the fallback in §5 rather than failing
loudly.

The validator (`MarkAsDisputedHandler.cs:7`) was deliberately **not** extended. It requires
`EmailInvitationId` and a non-empty `DisputeReason` under 255 characters; `DisputeCategory` is
optional, so a client that does not send it still files a dispute and still gets an acknowledgement.

## 4. The service: two sends, asymmetric on purpose

`DisputeOrderService.MarkAsDisputedAsync` (`:102`). Before the transaction it gathers what both
emails need — `requestor` from the token at `:129` (`ClaimTypes.Email`, falling back to the short
`"email"` claim), and `subjectName` from the order's name parts at `:131`.

The internal notification is unchanged, at `:139`: send first, and on failure log and throw
`InternalServerException`, so the write never happens.

Then the transaction, and after `CommitAsync` (`:170`):

```csharp
			var candidateName = string.IsNullOrWhiteSpace(subjectName)
				? order.EmailAddress ?? "this order"
				: subjectName;

			await _disputeEmailNotification.SendAsync(
				new DisputeEmailDetails(
					requestor,
					_currentUser.FullName,
					candidateName,
					disputeRequest.DisputeCategory,
					disputeRequest.DisputeReason!),
				cancellationToken);
```

Three things to notice:

- It is **inside** the `try` whose `catch` rolls back and rethrows. Safe only because `SendAsync`
  cannot throw (§5).
- `requestor` is reused rather than re-read, so both messages name the same address even on the
  fallback claim path that `MarkAsDisputedAsync_ShouldUseFallbackEmailClaim_WhenStandardEmailClaimIsMissing`
  pins.
- `DisputeReason!` — the null-forgiving is the same one the pre-existing internal send uses at
  `:139`; `MarkAsDisputedCommandValidator` has already rejected an empty reason.

`candidateName` falls back to the order's email address because the copy reads "A dispute has been
submitted for \<candidate\>", and an order with no name parts still has to identify someone.

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

   Comparing the two values rather than testing for `"Others"` is the point: that literal is a
   private const in the Blazor component and this service has no business knowing it. If the console
   ever adds a fourth category with free text, this keeps working.
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
| `DisputeReason`'s double meaning | The dialog, the notifier's derivation (§5 step 2), and `ATSRepository.DisputeOrders.cs:86` which writes it into `EmailInvitationRequest.DisputeCategory` | Nothing — see §8 |
| Route `/ats/markasdisputed` | `ATSPaths.cs:559` `MatchPath` vs. the UI's `PatchAsJsonAsync` vs. Carter's `MapPatch` + `PathSet` | Nothing at compile time |
| `IDisputeEmailNotification` | `ATSServiceConfiguration.cs:162`, registered beside the sender it depends on | DI |
| `IAtsEmailSender` in tests | The ATS integration host must register a fake implementing it — `FakeAtsEmailSender`, which gained `BuildDisputeNotification` | Nothing; see the withdrawal feature's `_code_explanation.md` §7.1 |

## 8. The column name lies, and that is pre-existing

`ATSRepository.DisputeOrders.cs:81` persists the request like this:

```csharp
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.DisputeCategory, disputeRequest.DisputeReason)
				.SetProperty(eir => eir.DisputedAt, DateTime.UtcNow),
				cancellationToken);
```

`DisputeReason` goes into the column named `DisputeCategory`. For Billing and Report the two are the
same string, so it reads correctly; for Others the column holds free text, not a category. This
predates the feature and was deliberately left alone — the disputes grid projects that column
(`ATSRepository.DisputeOrders.cs:39` → `DisputeOrderListDTO.DisputeCategory`) and changing what it
stores would change what the console shows. The new `DisputeCategory` request field is **not**
persisted, so none of this moved.

## 9. Tests

`Test/Test/BackendAPI/Modules/ATS.UnitTests/`:

| File | Covers |
|---|---|
| `DisputeEmailNotificationTests.cs` | Filer addressed and `clientsupport@cibi.com.ph` copied; details bullet rendered for an Others dispute and **omitted** when the reason equals the category; reason used as the category when none was sent; greeting falls back to the address; skipped when there is no address; **does not throw** when the sender throws or reports failure |
| `AtsDisputeEmailBodyTests.cs` | The composed body: exact copy, both contact addresses, header equals `DisputeEmail.Subject`, both bullets present with details and the details line absent without, values HTML-encoded, and every `href` is a `mailto:` |
| `DisputeOrderServiceTests.cs` — new `#region Requestor Acknowledgement` | Orchestration only: acknowledgement sent after the commit with the right values; category and reason passed through unmangled for Others; candidate falls back to the order's address; **not** sent when the operations email fails; **not** sent when the write fails |

The subject and the CC address are asserted as **literals**, not by reading `DisputeEmail`, so a
change to the agreed copy fails a test rather than silently redefining it.

The four pre-existing tests that assert the internal operations email
(`MarkAsDisputedAsync_ShouldSendNotificationUpdateRepositoryAndReturnTrue` and friends) were left
untouched and still pass — that message is unchanged. `DisputeOrderServiceIntegrationTests` stubs
`IDisputeEmailNotification`, so it keeps exercising the dispute write, the cache invalidation and the
operations email.

Not covered, and not coverable by unit tests: a successful SMTP send, so the `Cc:` header on the wire
needs the manual pass in the high-level document.

## 10. Change X, also check Y

| If you change… | Also check… |
|---|---|
| `DisputeEmail.Subject` | The header follows automatically; the **tests** do not — they pin the literal |
| `DisputeEmail.CopyTeam` | The closing sentence in `BuildDisputeNotification`, which names it as prose alongside `ccteam@cibi.com.ph` (§7) |
| The body copy | `AtsDisputeEmailBodyTests` asserts the sentences verbatim |
| The dialog's categories, or which ones allow free text | `DisputeEmailNotification.SendNoticeAsync` step 2 — it compares values rather than matching `"Others"`, so it should keep working, but `DisputeEmailNotificationTests` pins the Billing and Others cases |
| `DisputeOrderRequestDTO` on either side | The other side (§3), and `MarkAsDisputedCommandValidator` if the new field should be required |
| What `DisputeReason` means | Persistence at `ATSRepository.DisputeOrders.cs:86`, the operations email at `DisputeOrderService.cs:139`, and §8 |
| `IAtsEmailSender`'s members | `ATSEmailService`, `FakeAtsEmailSender` (the integration host — §7), and the mocks in `AtsEmailAccountManagementFixture.cs:34` |
| `DisputeOrderService`'s constructor | `DisputeOrderServiceTests.cs` and `DisputeOrderServiceIntegrationTests.cs:535` both news it up by hand |
| The transaction in `MarkAsDisputedAsync` | The acknowledgement must stay after `CommitAsync`, and `SendAsync` must stay unable to throw (§5) |
| `SendEmailForDispute` | It is the *internal* body on `IEmailService`, implemented three times: `ATSEmailService.cs:646`, `BuildingBlocks/…/EmailService.cs:215`, and `FakeEmailSender.cs:26`. Do not confuse it with `BuildDisputeNotification` |
