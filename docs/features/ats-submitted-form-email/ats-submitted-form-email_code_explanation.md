# ATS Submitted Form Email — Code Explanation

The call chain behind `ats-submitted-form-email.md`, traced hop by hop against the files as they now
stand. Read that document first for the *why*; this one is for changing the code.

## 1. The chain at a glance

| # | Hop | File | Member |
|---|---|---|---|
| 1 | Submit button | `UI/FrontendWebassembly/Component/ATS/ApplicationForm/ApplicationFormComponent.razor:1964` | `OnClick="OnSubmitForm"` |
| 2 | Validate + assemble | `…/ApplicationFormComponent.razor.cs:870` | `OnSubmitForm()`, service call at `:1001` |
| 3 | UI HTTP service | `UI/FrontendWebassembly/Services/ATS/ApplicationForm/ApplicationFormService.cs:12` | `AddApplicationFormDataAsync` → multipart `POST ats/addapplicationformdata` (`:210`) |
| 4 | Gateway route | `BackendAPI/Modules/ATS/Path/ATSPaths.cs:23` | `RouteId: "AddApplicationFormDataEntryPoint"` |
| 5 | Carter endpoint | `…/Features/Web/AddApplicationFormData/AddApplicationFormDataEndpoint.cs:20` | `MapPost("addapplicationformdata")` |
| 6 | Handler | `…/AddApplicationFormDataHandler.cs:669` | `AddApplicationFormDataHandler` |
| 7 | Submission write | `…/Services/ApplicationForm/ApplicationFormService.cs:64` | `AddApplicationFormDataAsync` |
| 7a | Authorization gate | `…/ApplicationFormService.cs:186` | `AuthorizeApplicationFormAsync` |
| 7b | In-app notification (pre-existing) | `…/ApplicationFormService.cs:133` | `RaiseForOrderAsync` |
| 8 | **The notice** | `…/ApplicationFormService.cs:147` → `Services/EmailService/SubmittedFormEmailNotification.cs:28` | `SendAsync` → `SendNoticeAsync:47` |
| 9 | Body | `…/Services/EmailService/ATSEmailService.cs:675` | `BuildSubmittedFormNotification` |
| 10 | Send | `…/ATSEmailService.cs:61` | `SendATSEmailWithResultAsync` → `SendThroughAccountAsync:134` → `SendOverContextAsync:240` → `BuildMessage:386` |

Steps 1–7b all existed before this feature. Step 8 is the only new call; step 9 is the only new
member on the sender. Step 10's `cc` parameter was added by the withdrawal notice and is reused
unchanged — this feature made no change to the send path at all.

## 2. The gate that makes the notice single-use

`AuthorizeApplicationFormAsync` (`ApplicationFormService.cs:186`) runs before anything is written:

```csharp
		// Withdrawn and Done are both terminal. Without this the second post would fail
		// on the PersonalDetails 1:1 unique constraint as an opaque 500.
		if (!string.Equals(claim.ApplicationFormStatus, ApplicationFormStatus.Pending, StringComparison.OrdinalIgnoreCase))
			throw new ConflictException($"This application form has already been {claim.ApplicationFormStatus?.ToLowerInvariant() ?? "processed"}.");
```

and `UpdateEmailInvitationRequestForFilledUpFormAsync`
(`ATSRepository.ApplicationForms.cs:96`) sets `ApplicationFormStatus = Done` inside the same
transaction as the commit. The notice therefore cannot fire twice for one order, and there is no
idempotency state to maintain. The same predicate is what `IsHashTokenValidAsync` answers for
PhilSys, so the two agree by construction.

## 3. Where the send sits, and why it must not throw

After `CommitAsync` (`:125`), the method already had a best-effort side effect — the in-app
notification at `:133`. The email is added immediately after it, at `:147`:

```csharp
			await _submittedFormEmailNotification.SendAsync(
				new SubmittedFormEmailDetails(
					emailInvitationId,
					$"{personalDetails.FirstName} {personalDetails.LastName}".Trim()),
				ct);
```

Two things about that placement:

- It is **inside** the `try`, whose `catch` iterates `uploadedKeys.All` and deletes every stored
  file as compensation. That is safe only because `SendAsync` cannot throw. An escaping exception
  here would not merely report a false failure — it would remove the attachments belonging to a
  form that is already saved.
- Only the **order id** and the **name from the form** are passed. Everything else the notice needs
  (`RequestorId`, the candidate's mailbox) is on the order row, which the notifier loads itself.
  `personalDetails` is in scope here and is the authoritative source for the name, so it is read
  rather than re-fetched.

## 4. `SubmittedFormEmailNotification`

Two files under `Services/EmailService/`, following the module's one-type-per-file convention:

- `ISubmittedFormEmailNotification.cs` — the contract, plus the `SubmittedFormEmailDetails` record
  (`:43`). Its remarks record the non-obvious part: the name comes from the form, the mailbox comes
  from the row, because the form has no primary email address.
- `SubmittedFormEmailNotification.cs` — the implementation. Takes `ILogger`, `IAtsEmailSender`,
  `IAuthQueries` and `IATSRepository`.

```csharp
	public async Task SendAsync(
		SubmittedFormEmailDetails details,
		CancellationToken cancellationToken)
	{
		// Guarded here rather than at the call site, matching the other two notices. Beyond the
		// usual "the work is already durable" reasoning, this caller's catch block deletes the
		// uploaded attachments as compensation - so an exception escaping after the commit would
		// destroy the files belonging to a form that is already saved.
		await SideEffectGuard.RunAsync(
			() => SendNoticeAsync(details, cancellationToken),
			_logger,
			$"notify the requestor that the application form for order {details.EmailInvitationId} was completed",
			cancellationToken);
	}
```

`SendNoticeAsync` (`:47`) throws freely *because* its only caller is that guard. In order:

1. **Load the order.** `_atsRepository.GetEmailInvitationRequestByIdAsync(id, ct)`, through the
   cached decorator — the same shape as `AtsNotificationService.RaiseForOrderAsync`, which also
   takes an id and resolves its own target. **Trap:** that repository method returns an *empty
   placeholder* rather than `null` when the id matches nothing, so a missing order arrives looking
   like an order with no requestor and no address. Step 2's guard covers both, which is why there is
   no separate null check.
2. **Bail if there is nobody to address.** `!invitation.RequestorId.HasValue` → log and return.
   Public-API orders carry no requestor id.
3. **Resolve the mailbox.** `IAuthQueries.GetATSAssignedUserAsync(invitation.RequestorId.Value, ct)`
   → `ATSUserLookupDTO`. `EmailInvitationRequest.Requestor` is a display name, not an address; this
   is the only source of the mailbox, and it is the same lookup the withdrawal notice and the OMS
   ticketing processor make.
4. **Pick the greeting name.** Directory `UserName`, then the order's `Requestor`, then the mailbox.
5. **Pick the candidate name** — `ResolveCandidateName` (`:141`):

```csharp
		if (!string.IsNullOrWhiteSpace(submittedName))
		{
			return submittedName.Trim();
		}

		var storedName = $"{invitation.FirstName} {invitation.LastName}".Trim();

		return string.IsNullOrWhiteSpace(storedName)
			? invitation.EmailAddress ?? "the candidate"
			: storedName;
	}
```

   Submitted name wins; the order row is the fallback; the mailbox is the last resort.
6. **Build the copy list.**

```csharp
		var cc = new List<string>(SubmittedFormEmail.CopyTeams);

		if (!string.IsNullOrWhiteSpace(invitation.EmailAddress))
		{
			cc.Add(invitation.EmailAddress);
		}
```

   `new List<string>(...)` rather than a collection expression, because `CopyTeams` is a shared
   `static readonly` collection and appending to it directly would mutate it for every later caller.
7. **Send** with `subject: SubmittedFormEmail.Subject` and `cc`, then log. A `!result.IsSent` is a
   warning, not an exception — `Throttled` means defer, and there is no queue behind this notice.

## 5. The composer and the constants

`ATSEmailService.BuildSubmittedFormNotification` (`:675`), declared on `IAtsEmailSender` beside the
withdrawal and dispute composers. Same card markup, same contact block, same confidentiality footer;
both interpolated names HTML-encoded. Its header reads the shared constant:

```csharp
						<h1 style='margin:0;font-size:20px'>{SubmittedFormEmail.Subject}</h1>
```

`Constants/SubmittedFormEmail.cs` holds `Subject` (`:23`) and `CopyTeams` (`:38`). The subject says
"In Progress" rather than "Submitted" because that is the `OrderStatus` the submission sets, so the
email and the console grid agree.

## 6. Wiring that is not visible from any one file

| Thing | Where it must agree | Enforced by |
|---|---|---|
| Subject vs. body header | `Constants/SubmittedFormEmail.cs:23`, read by `SubmittedFormEmailNotification.SendNoticeAsync` and by `BuildSubmittedFormNotification` | The shared constant only |
| CC list vs. body copy | `CopyTeams` = `ccteam` + `pre-workteam`; the closing sentence names `ccteam` **and** `clientsupport` | **Nothing** — and they genuinely disagree (§7) |
| Candidate name source | `ApplicationFormService.cs:147` passes `personalDetails`; `ResolveCandidateName` falls back to the row | Nothing — the fallback is silent |
| Route `/ats/addapplicationformdata` | `ATSPaths.cs:23` `MatchPath` vs. the UI's `PostAsync` (`ApplicationFormService.cs:210`) vs. Carter's `MapPost` + `PathSet` | Nothing at compile time |
| `ISubmittedFormEmailNotification` | `ATSServiceConfiguration.cs:163`, registered beside the sender it depends on | DI |
| `IAtsEmailSender` in tests | The ATS integration host must register a fake implementing it — `FakeAtsEmailSender`, which gained `BuildSubmittedFormNotification` | Nothing; see the withdrawal feature's `_code_explanation.md` §7.1 |
| Single-use submission | `AuthorizeApplicationFormAsync` (`:186`), `UpdateEmailInvitationRequestForFilledUpFormAsync`, and `IsHashTokenValidAsync` all key off `ApplicationFormStatus = Pending` | The shared constant, and a comment in the repository |

## 7. The CC list and the body copy disagree, deliberately

`CopyTeams` puts `ccteam@cibi.com.ph` and `pre-workteam@cibi.com.ph` on the message. The body's
closing sentence tells the reader to contact `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`.

So `clientsupport` is named in the text but not copied, and `pre-workteam` is copied but not named.
Both sides are reproduced exactly as agreed, and neither is a typo to "fix" unilaterally — the same
kind of mismatch exists in the dispute notice. If either list changes, change the constant *and* the
sentence in `BuildSubmittedFormNotification`, and expect `AtsSubmittedFormEmailBodyTests` and
`SubmittedFormEmailNotificationTests` to fail until both are updated: they pin the literals on
purpose.

## 8. Tests

`Test/Test/BackendAPI/Modules/ATS.UnitTests/`:

| File | Covers |
|---|---|
| `SubmittedFormEmailNotificationTests.cs` | Requestor addressed with both teams **and** the candidate copied, in that order; submitted name beats the name on the order row; falls back to the row, then to the mailbox; candidate left off the copy when the row has no address; skipped when there is no `RequestorId`, when the order cannot be found (the empty-placeholder case), or when the requestor is not in the directory; **does not throw** when the sender throws or reports failure |
| `AtsSubmittedFormEmailBodyTests.cs` | The composed body: exact copy, both contact addresses, header equals `SubmittedFormEmail.Subject`, names HTML-encoded, and every `href` is a `mailto:` — i.e. no download link |

The subject and the two team addresses are asserted as **literals**, not by reading
`SubmittedFormEmail`, so a change to the agreed copy fails a test rather than silently redefining
it.

The submit path's own wiring is exercised by the pre-existing `AddApplicationFormDataIntegrationTests`
through the real container, which is what catches a missing `FakeAtsEmailSender` member. Those tests
were not modified.

Not covered, and not coverable by unit tests: a successful SMTP send, so the three-address `Cc:`
header on the wire and the `RecipientCount = 4` write to `ats."EmailSendLog"` both need the manual
pass in the high-level document.

## 9. Change X, also check Y

| If you change… | Also check… |
|---|---|
| `SubmittedFormEmail.Subject` | The header follows automatically; the **tests** do not — they pin the literal |
| `SubmittedFormEmail.CopyTeams` | The body's closing sentence in `BuildSubmittedFormNotification` (§7), and the expected `Cc:` sequence in `SubmittedFormEmailNotificationTests` |
| The body copy | `AtsSubmittedFormEmailBodyTests` asserts the sentences verbatim |
| `SubmittedFormEmailDetails` | Its single caller at `ApplicationFormService.cs:147`, and every test in `SubmittedFormEmailNotificationTests` |
| Where the candidate name comes from | `ResolveCandidateName` (`:141`) and the three name tests; the form still has no primary email field, so the mailbox must keep coming from the row |
| `ApplicationFormService`'s constructor | `ApplicationFormServiceWithdrawnEmailTests.cs:56` news it up by hand — the only place in the repo that does |
| `AddApplicationFormDataAsync`'s transaction or its compensation `catch` | The notice must stay after `CommitAsync`, and `SendAsync` must stay unable to throw (§3) |
| `ApplicationFormStatus` or `AuthorizeApplicationFormAsync` | The single-use guarantee in §2, plus `IsHashTokenValidAsync` and the follow-up chaser's `ApplicationFormStatus = Pending` filter |
| `IAtsEmailSender`'s members | `ATSEmailService`, `FakeAtsEmailSender` (the integration host), and the mocks in `AtsEmailAccountManagementFixture.cs:34` |
| `GetEmailInvitationRequestByIdAsync`'s not-found behaviour | §4 step 1 — the empty-placeholder return is what makes the missing-order case safe |
