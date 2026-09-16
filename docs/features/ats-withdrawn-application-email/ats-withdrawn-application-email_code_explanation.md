# ATS Withdrawn Application Email — Code Explanation

The call chain behind `ats-withdrawn-application-email.md`, traced hop by hop against the files as
they now stand. Read that document first for the *why*; this one is for changing the code.

## 1. The chain at a glance

| # | Hop | File | Member |
|---|---|---|---|
| 1 | The button | `UI/FrontendWebassembly/Component/ATS/ApplicationForm/ApplicationFormComponent.razor:221` | `OnClick="CancelTransaction"` |
| 2 | Confirm, then call | `…/ApplicationFormComponent.razor.cs:355` | `CancelTransaction()` |
| 3 | UI HTTP service | `UI/FrontendWebassembly/Services/ATS/ApplicationForm/ApplicationFormService.cs:255` | `WithdrawApplicationForm(hashToken)` |
| 4 | Gateway route | `BackendAPI/Modules/ATS/Path/ATSPaths.cs:648` | `RouteId: "WithdrawnApplicationForm"` |
| 5 | Carter endpoint | `…/Features/Web/WithdrawnApplicationForm/WithdrawnApplicationFormEndpoint.cs:11` | `MapPatch("withdrawnapplicationform")` |
| 6 | Validator + handler | `…/WithdrawnApplicationFormHandler.cs:8`, `:25` | `WithdrawnApplicationFormCommandValidator`, `Handle` |
| 7 | Withdrawal write | `…/Services/ApplicationForm/ApplicationFormService.cs:480` | `WithdrawnApplicationForm` |
| 8 | **The notice** | `…/Services/EmailService/WithdrawnEmailNotification.cs:25`, contract in `IWithdrawnEmailNotification.cs` | `SendAsync` → `SendNoticeAsync:53` |
| 9 | Requestor lookup | `BackendAPI/Modules/Auth/Data/Repository/UserDirectory/AuthRepository.UserDirectory.cs:91` | `GetATSAssignedUserAsync` |
| 10 | Body | `…/Services/EmailService/ATSEmailService.cs:552` | `BuildWithdrawnApplicationNotification` |
| 11 | Send | `…/ATSEmailService.cs:61` | `SendATSEmailWithResultAsync` → `SendThroughAccountAsync:134` → `SendOverContextAsync:240` → `BuildMessage:386` |

Steps 1–7 all existed before this feature. Step 8 is the only new call; 9–11 are existing machinery
that gained one optional parameter.

## 2. The button is labelled "Cancel", not "Withdraw"

Worth stating because it is the first thing a future reader gets wrong. There is no button in the
markup that says Withdraw:

```razor
<MudButton Class="ats-step-one-secondary-btn"
           Disabled="@(!declineConsent || consent)"
           OnClick="CancelTransaction">
    Cancel
</MudButton>
```

It sits on the consent step and is only enabled once the candidate has *declined* consent. Its
handler is the method the feature is named after, `CancelTransaction`
(`ApplicationFormComponent.razor.cs:355`), which opens a `YesNoDialogComponent` titled "Withdraw
Application" and only then calls the API:

```csharp
		var withdrawResponse = await ATSService.WithdrawApplicationForm(HashToken!);

		if (!withdrawResponse.IsSuccess)
		{
			Snackbar.Add(withdrawResponse.ErrorDetail, Severity.Error);
			return;
		}

		if (!withdrawResponse.Data)
			return;

		await IsWithDrawn.InvokeAsync("Withdrawn");
		await RemoveItemsAsync();
```

Nothing here changed. The UI cannot tell whether the notice went out, and deliberately does not:
the send is best-effort on the server (§4).

## 3. The endpoint and handler are pass-through

`WithdrawnApplicationFormEndpoint.cs` is anonymous — authorization *is* the emailed hash token — and
rate-limited by the gateway:

```csharp
		app.MapPatch("withdrawnapplicationform", async (WithdrawnApplicationFormRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new WithdrawnApplicationFormCommand(request.HashToken);
			WithdrawnApplicationFormResult result = await sender.Send(command, cancellationToken);
			var response = new WithdrawnApplicationFormResponse(result.isEdited);
			return Results.Ok(response.isEdited);

		})
		.AllowAnonymous()
```

The matching gateway entry (`ATSPaths.cs:648`) forwards `/ats/withdrawnapplicationform` with
`PathSet` to `/withdrawnapplicationform` and carries
`RateLimitPolicy = GatewayConstants.RateLimitPolicies.AnonymousApplicationForm`. **Both already
existed** — this feature added no route, so there is nothing new to look for in `GET /__routes`.

`WithdrawnApplicationFormHandler.cs:25` does one thing:

```csharp
		var isEdited = await _applicationFormService.WithdrawnApplicationForm(request.HashToken, cancellationToken);
		return new WithdrawnApplicationFormResult(isEdited);
```

The validator beside it only asserts a non-empty `HashToken`. No new fields, so no validator change.

## 4. The withdrawal write, and the one added line

`Services/ApplicationForm/ApplicationFormService.cs:480`. The invitation is loaded *before* the
transaction, which is what makes the notice cheap — every field it needs is already in memory:

```csharp
		var invitationInfo = await _atsRepository.GetEmailIdAndApplicationFormPathAsync(hashToken, ct);
		var invitation = invitationInfo.EmailId == Guid.Empty
			? new EmailInvitationRequest()
			: await _atsRepository.GetEmailInvitationRequestByIdAsync(invitationInfo.EmailId, ct);
```

Then, after `CommitAsync`:

```csharp
			await _unitOfWork.CommitAsync(ct);

			// After the commit, deliberately - the same reasoning as the submission path above.
			// The withdrawal is the thing that matters and it is now durable; telling the requestor
			// is a best-effort follow-up that must not be able to roll it back. The notifier guards
			// itself, exactly as RaiseForOrderAsync does, so a delivery failure is logged there and
			// the candidate still sees their withdrawal succeed.
			await _withdrawnEmailNotification.SendAsync(invitation, ct);

			return true;
```

Two things about that placement are load-bearing and easy to break:

- It is **inside** the `try`, whose `catch` calls `RollbackAsync` and rethrows. That is safe only
  because `SendAsync` cannot throw — see §5. Remove the guard and a delivery failure would trigger a
  rollback *after* a successful commit, then surface as a 500.
- It is **after** `CommitAsync`. `WithdrawnApplicationForm_ShouldNotNotify_WhenTheCommitFails` pins
  this: it makes the commit throw and asserts the notifier was never called.

Note the pre-existing `catch`/`RollbackAsync` in this method is hand-rolled rather than
`TransactionRunner`. That predates this feature and was deliberately left alone; converting it is a
separate change (see the migration checklist in `docs/features/transaction-runner/`).

## 5. `WithdrawnEmailNotification` — the new files

Two files under `Services/EmailService/`, following the module's one-type-per-file convention
(`IAtsEmailSender.cs` beside `ATSEmailService.cs`):

- `IWithdrawnEmailNotification.cs` — the contract. One method, and its remarks carry the rule that
  matters: an implementation must never let a delivery failure escape, because every caller is
  finishing work that has already committed.
- `WithdrawnEmailNotification.cs` — the implementation, registered in `ATSServiceConfiguration`.

```csharp
	public async Task SendAsync(
		EmailInvitationRequest invitation,
		CancellationToken cancellationToken)
	{
		// Guarded here rather than at the call site, matching AtsNotificationService. If this threw
		// and reached CustomExceptionHandler the candidate would get a 500 for a withdrawal that
		// already committed - and would read it as "my withdrawal failed".
		await SideEffectGuard.RunAsync(
			() => SendNoticeAsync(invitation, cancellationToken),
			_logger,
			$"notify the requestor that invitation {invitation.EmailInvitationID} was withdrawn",
			cancellationToken);
	}
```

`SendNoticeAsync` (`:53`) throws freely *because* its only caller is that guard. In order:

1. **Bail if there is nobody to address.** `!invitation.RequestorId.HasValue` → log and return.
   Public-API orders carry no requestor id.
2. **Resolve the mailbox.** `IAuthQueries.GetATSAssignedUserAsync(invitation.RequestorId.Value, ct)`
   returns `ATSUserLookupDTO?`. `EmailInvitationRequest.Requestor` is a *display name*, not an
   address — this lookup is the only source of the mailbox. Bail if `requestor?.UserEmail` is blank.
3. **Pick the greeting name.** The directory's joined `UserName` wins; `invitation.Requestor` is the
   fallback for a user the ATS-assignment filter no longer returns; the mailbox is the last resort.
4. **Compose.** `_emailSender.BuildWithdrawnApplicationNotification(requestorName, BuildCandidateName(invitation))`.
   `BuildCandidateName` (`:128`) joins `FirstName`/`LastName` and falls back to
   `invitation.EmailAddress` — the copy reads "Your candidate, \<name\>, has withdrawn", so an
   address still identifies someone where an empty name does not.
5. **Build the copy list.**

```csharp
		var cc = new List<string> { WithdrawnEmail.CopyTeam };

		if (!string.IsNullOrWhiteSpace(invitation.EmailAddress))
		{
			cc.Add(invitation.EmailAddress);
		}
```

   A missing candidate address leaves them off the copy rather than failing the send — the requestor
   is still owed the notice.
6. **Send** through `SendATSEmailWithResultAsync`, then log the outcome. A `!result.IsSent` is a
   warning, not an exception: `Throttled` means "defer", and there is no queue behind a withdrawal.

## 6. The sender changes

`IAtsEmailSender` gained an optional trailing `cc` on two methods, and a new composer. Trailing is
not a style choice — C# requires optional parameters last, and it is what kept every existing caller
compiling:

```csharp
	Task<EmailDeliveryResult> SendATSEmailWithResultAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null);
```

`cc` threads `SendATSEmailWithResultAsync:61` → `SendThroughAccountAsync:134` →
`SendOverContextAsync:240` → `BuildMessage:386`. `SendWithCredentialsAsync` was left alone: it
proves credentials during account registration and has no copy list.

Two details in `SendThroughAccountAsync` matter:

```csharp
		// Normalised once here rather than inside the message builder, so the recipient count
		// charged to this account's daily cap is provably the same list that goes on the wire.
		var copied = NormalizeRecipients(cc);
```

```csharp
			// Recipients, not messages: the TO address plus everyone copied. The provider counts
			// recipients against the daily cap, so a copied message has to consume more of it -
			// see AtsEmailSendLog.RecipientCount, which is summed rather than counted.
			await _poolRegistry.ReportSuccessAsync(accountId, 1 + copied.Count, cancellationToken);
```

That `1` used to be a literal. `NormalizeRecipients` (private static, `ATSEmailService.cs:421`) drops
null/blank entries and trims, so the count and the message cannot disagree. `BuildMessage` adds each
remaining address to `message.Cc`.

`BuildWithdrawnApplicationNotification` (`:552`) sits beside `BuildApplicationFormReminderNotification`
and reuses its card markup verbatim. It HTML-encodes both names, and its header reads the shared
constant:

```csharp
						<h1 style='margin:0;font-size:20px'>{WithdrawnEmail.Subject}</h1>
```

## 7. Wiring that is not visible from any one file

| Thing | Where it must agree | Enforced by |
|---|---|---|
| Subject line vs. body header | `Constants/WithdrawnEmail.cs` `Subject`, read by `WithdrawnEmailNotification.SendNoticeAsync` and by `BuildWithdrawnApplicationNotification` | The shared constant only |
| `ccteam@cibi.com.ph` | `WithdrawnEmail.CopyTeam`, and the *body copy* which names `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph` as prose | Nothing — the body text is a separate literal |
| Route `/ats/withdrawnapplicationform` | `ATSPaths.cs:648` `MatchPath` vs. the UI's `_httpClient.PatchAsJsonAsync($"ats/withdrawnapplicationform")` vs. Carter's `MapPatch` + `PathSet` | Nothing at compile time |
| `IAtsEmailSender` resolution | `ATSServiceConfiguration.cs:156` registers it by casting the **keyed** `"ats"` `IEmailService` | Runtime cast — see §7.1 |
| `IWithdrawnEmailNotification` | `ATSServiceConfiguration.cs`, registered immediately after the sender it depends on | DI |
| Recipient count vs. copy list | `1 + copied.Count` in `SendThroughAccountAsync` vs. what `BuildMessage` puts on the wire | `NormalizeRecipients` being called once, in the same method |

The `ccteam@cibi.com.ph` duplication is the one to watch: it appears both as the CC address and
inside the body's contact sentence. Changing one without the other produces an email that tells the
recipient to contact a mailbox that was not copied.

### 7.1 The keyed cast, and why the integration host needs its own fake

`IAtsEmailSender` is not registered as itself. It is a cast:

```csharp
		services.AddScoped<IAtsEmailSender>(provider =>
			(IAtsEmailSender)provider.GetRequiredKeyedService<IEmailService>("ats"));
```

In production the keyed `"ats"` service is `ATSEmailService`, which implements both interfaces, so
the cast holds. The ATS integration host replaces that registration with a fake
(`Test/Test/BackendAPI/Infrastructure/ATS.Infrastracture/IntegrationTestWebAppFactory.cs:91`), and a
fake implementing only `IEmailService` makes the cast throw — **at resolve time, not send time**.

That distinction is what makes it expensive. The exception surfaces wherever the container builds a
service injecting `IAtsEmailSender` directly, so the tests that fail are not the email tests: adding
this feature broke `WithdrawnApplicationFormIntegrationTests`,
`GetEmailIdAndApplicationFormPathIntegrationTests` and `AddApplicationFormDataIntegrationTests` — all
persistence tests — because all three resolve `IApplicationFormService`, which now depends on
`IWithdrawnEmailNotification`.

`FakeAtsEmailSender` (same folder) exists for this reason: it derives from the shared
`FakeEmailSender`, adds the five `IAtsEmailSender` members, and still puts nothing on the wire. The
Auth host keeps the plain fake, which is all it needs.

`EndorsementSubmissionService` survives the same cast because it deliberately uses `as` and degrades:

```csharp
		var resultAwareSender = _emailService as IAtsEmailSender;
```

That works only because it has a fallback body on `IEmailService`. A withdrawal notice has no
fallback — its composer exists solely on `IAtsEmailSender` — so `WithdrawnEmailNotification` depends
on the interface directly and the fake has to satisfy it. **Any new ATS service that injects
`IAtsEmailSender` inherits this constraint.**

## 8. Tests

`Test/Test/BackendAPI/Modules/ATS.UnitTests/`:

| File | Covers |
|---|---|
| `ApplicationFormServiceWithdrawnEmailTests.cs` | The seam this service owns: notice sent with the loaded invitation after a successful commit; **not** sent when the commit throws; **not** sent when the token matches no row |
| `WithdrawnEmailNotificationTests.cs` | Recipient and copy list; greeting name from the directory and its two fallbacks; skip when there is no `RequestorId` or no directory entry; candidate left off the copy when the row has no address; **does not throw** when the sender throws or reports failure |
| `AtsWithdrawnEmailBodyTests.cs` | The composed body: exact copy, both contact addresses, header equals `WithdrawnEmail.Subject`, names HTML-encoded, and every `href` is a `mailto:` — i.e. no application-form button |

The subject and the CC address are asserted as **literals**, not by reading `WithdrawnEmail`. A test
that read the constant would keep passing if someone changed the agreed copy, which is the one thing
it exists to catch.

Not covered, and not coverable by unit tests: a successful SMTP send, so the `Cc:` header on the
wire and the `RecipientCount = 3` write to `ats."EmailSendLog"` both need the manual pass in the
high-level document. `SmtpLease` wraps a real `SmtpClient`; this is the same reason
`AtsEmailFailoverTests` injects its failures through `GetContextAsync`.

The existing integration tests cover the withdrawal write itself
(`WithdrawnApplicationFormIntegrationTests`) and exercise the new dependency graph through the real
container — which is how §7.1 was found. They do **not** assert on the notice, because the host's
`FakeAtsEmailSender` discards what it is given.

**Flaky, unrelated:** `ResendApplicationFormIntegrationTests` (3 tests) fails intermittently with
"Failed to find email invitation for resend". Observed failing in three of four full `~ATS` runs
while this feature was in progress — including at pristine `HEAD` with every change stashed, and in
an isolated run of that class alone — then passing twice consecutively after a change that could not
have affected it (splitting an interface into its own file). Not caused by the withdrawal notice and
left alone; re-run before concluding anything from it.

## 9. Change X, also check Y

| If you change… | Also check… |
|---|---|
| `WithdrawnEmail.Subject` | The body header follows automatically. The **tests** do not — they pin the literal, so update `WithdrawnEmailNotificationTests.WithdrawnSubject` and `AtsWithdrawnEmailBodyTests` |
| `WithdrawnEmail.CopyTeam` | The body's contact sentence in `BuildWithdrawnApplicationNotification`, which names it as prose (§7) |
| The body copy | `AtsWithdrawnEmailBodyTests` asserts the sentences verbatim |
| `SendATSEmailWithResultAsync`'s signature | `ATSEmailService.cs:29`, `EndorsementSubmissionService.cs:407`, `WithdrawnEmailNotification.SendNoticeAsync`, and four calls in `AtsEmailFailoverTests.cs` |
| `SendThroughAccountAsync` | It is called from exactly one place — the switcher at `ATSEmailService.cs:105`. Its XML doc still claims registration uses it; it does not (finding C8 in `docs/features/ats-email-delivery/`) |
| `ReportSuccessAsync`'s recipient count | `AtsEmailSendLog.RecipientCount`, `AtsEmailAccountRepository.BuildSnapshotQuery`, and the cap figure the UI shows |
| `BuildMessage` | Every send path funnels through it, including the OTP verification path |
| `EmailInvitationRequest.RequestorId` or the Auth directory | `WithdrawnEmailNotification.SendNoticeAsync` step 2, and `OMSTicketingProcessorService.cs:160`, which resolves the requestor the same way |
| `WithdrawnApplicationForm`'s transaction | The notice must stay after `CommitAsync`, and `SendAsync` must stay unable to throw (§4) |
| `ApplicationFormStatus.Withdrawn` or the follow-up predicate | `ReleaseDueFollowUpInvitationsAsync` (`ATSRepository.EmailInvitations.cs:160`) filters `ApplicationFormStatus = Pending`; widening it would start chasing withdrawn candidates |
| `IAtsEmailSender`'s members | Three places implement or mock it: `ATSEmailService`, `FakeAtsEmailSender` (the integration host — §7.1), and the `Mock<IAtsEmailSender>` in `AtsEmailAccountManagementFixture.cs:34`. The shared `FakeEmailSender` implements only `IEmailService` and must stay that way; the Auth host depends on it |
| The gateway route or the UI URL | All three strings in §7 |
