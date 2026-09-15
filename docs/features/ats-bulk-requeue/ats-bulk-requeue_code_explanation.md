# ATS Bulk Requeue — Code Explanation

Companion to [`ats-bulk-requeue.md`](ats-bulk-requeue.md). That document explains *why* bulk
retry is shaped the way it is — why a stale selection is skipped rather than rejected, why the
cap exists, why the action lives in its own band. This one is for someone about to change the
implementation: it walks the two real call chains hop by hop, names the file and method at each
one, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map — §10 answers *"I'm changing X, what else
touches it?"*.

> **Read §0 first.** The design doc is wrong or incomplete in eleven places, and one of them
> (C1) misquotes the single most important predicate in the feature. Every claim below was
> verified against the code on branch `feature/Update-ReadMe-File`; where the two disagree, this
> document follows the code.
>
> The **Ticketing** board's bulk retry is also covered by
> [`oms-auto-ticketing_code_explanation.md`](../oms-auto-ticketing/oms-auto-ticketing_code_explanation.md)
> §4 (the slice) and §6.4 (the UI). This document traces the **Bulk Uploads** side in full and
> gives the ticketing side as a diff, so the two do not drift apart.

---

## 0. Where the design doc no longer matches the code

| # | `ats-bulk-requeue.md` says | The code actually does |
|---|---|---|
| **C1** | §2 shows one predicate and says it lives "in every one of those": `ids.Contains(x.EmailInvitationID) && !x.IsTicketed && x.TicketStatus == TicketStatus.Error && x.TicketAttempts >= MaxTicketAttempts` | That is the **ticketing** predicate only. `RequeueEmailInvitationsAsync` (`ATSRepository.EmailInvitations.cs:108`) matches `x.EmailInvitationID == requeue.EmailInvitationId && x.EmailSentStatus != EmailStatus.Processing` — **one id per statement**, and a *negative* guard with no terminal-state check. The two sides do **not** follow the same pattern. Full comparison in §4. |
| **C2** | §1 "selecting many **failed** rows" | The Bulk Uploads side also offers rows whose email was **delivered**. `CanResend` returns `true` for `EmailSentStatus == Done` (`BulkUploadSubjectsDialog.razor.cs:491-513`, comment: *"Delivered but not acted on, or withdrawn: a nudge is legitimate."*). Requeueing one re-sends an email that already went out. Intended, but the doc's framing hides it — see §8.1. |
| **C3** | §3 "Below 600px it reverts to `position: static`" | True for the Ticketing board (`ats.css:1032` opens the block, rule at `:1058`). The dialog's restated copy breaks at **720px** (`BulkUploadSubjectsDialog.razor.css:445`, rule at `:462`). The doc's own "keep the two in step" instruction is already violated. |
| **C4** | §3 "The Bulk Uploads dialog restates the same rules locally … because it is not rendered inside `.ats-management-page` and so cannot match that rule's page anchor" | It **is** rendered inside one: `BulkUploadSubjectsDialog.razor:17` is `<div class="ats-management-page ats-bulk-subjects-shell ats-dialog-shell">`. The duplication is real; the stated reason is not. The same incorrect sentence is copied into the CSS at `BulkUploadSubjectsDialog.razor.css:128-132` and again at `:429-431`. |
| **C5** | §5 file list | Omits `Data/Cache/EmailInvitations/ATSCacheRepository.EmailInvitations.Cache.cs`, which wraps `RequeueEmailInvitationsAsync` and revokes the `Report` and `WithdrawnApplication` HybridCache tags whenever anything moved. **NOT FOUND** in the doc. |
| **C6** | §2 names two pre-update reads: `GetRetryTargetsAsync` and `GetEmailInvitationOwnersAsync` | The ticketing bulk path makes a **third** read the doc never mentions: `GetExhaustedTicketIdsAsync` (`OMSTicketingRepository.cs:210`), used solely to decide which ids get history entries. **NOT FOUND** in the doc. |
| **C7** | §2 "The one case that does throw is `NotFoundException`" | **Three** throw: `BadRequestException` above the cap, `ForbiddenException` when the caller has no ATS access at all, and `NotFoundException` when nothing in the selection is in scope. The doc's own endpoint table in §2 lists 400 and 403, so §2's prose contradicts it. |
| **C8** | §2 does not discuss bulk history writes at all | Both paths call `OrderHistoryService.RecordManyAsync`, but for **different id sets**: ticketing records `eligibleIds` (a pre-update read), email records `inScopeIds` — which includes rows that did **not** move. Neither is the set the `UPDATE` actually touched. See §8.3 and §8.4. |
| **C9** | §2 implies `RequestedCount` is what the caller posted | Both services call `.Distinct()` first, so `RequestedCount` is the **deduplicated** count. 500 posted ids containing 480 distinct ones reports `RequestedCount = 480`. Not mentioned anywhere in the doc. |
| **C10** | §5 lists `Component/ATS/BulkUploads/BulkUploadSubjectsDialog` | Correct — and worth stating explicitly because the filename misleads: **`BulkUploadsComponent.*` (the board itself) has no selection state and makes no requeue call at all.** Its only involvement is `await dialog.Result;` followed by `ReloadTableAsync()` (`BulkUploadsComponent.razor.cs:119-124`). The bulk requeue lives entirely in the drill-down dialog. |
| **C11** | §6 lists four tests that "pin the design decisions" | All four exist and were read. But there is **no test anywhere for `RetryTicketsAsync`** — the ticketing bulk *service* method (grep `RetryTicketsAsync` across `Test/` returns zero hits; `OMSTicketingMonitoringServiceTests.cs` covers only the single-row `RetryTicketAsync`). And **no** test pins the "only the first of two racing calls wins" property for either *bulk* requeue — only the single-row `RequeueExhaustedTicketAsync_ShouldBeIdempotent_WhenCalledTwice`. **NOT FOUND**. |

Two things the doc describes accurately and that the code confirms without qualification: the
one-token-per-invitation rule (§7 of the doc, `ATSRepository.EmailInvitations.cs:120-124`) and
the 500 cap being enforced in both the validator and the service (§2 of the doc).

---

## 1. Two slices, one shape, no shared code

There is no `Features/Web/BulkUploads/` folder and no `Requeue*` folder. The two requeue slices
live in unrelated places because they belong to two different features that converged on one UI
shape; nothing in the backend is shared between them — no common base handler, no common
service, no common DTO beyond `BulkRetryResultDTO`.

| | Bulk Uploads Status | Ticketing Status |
|---|---|---|
| Slice folder | `BackendAPI/Modules/ATS/Features/Web/ResendApplicationForms/` | `BackendAPI/Modules/ATS/Features/Web/OMSTicketing/Command/RetryTickets/` |
| Carter route | `app.MapPatch("resendapplicationforms", …)` | `app.MapPatch("retrytickets", …)` |
| Gateway route | `/ats/resendapplicationforms` (`ATSPaths.cs:614-622`) | `/ats/retrytickets` (`ATSPaths.cs:438-446`) |
| Command | `ResendApplicationFormsCommand` | `RetryTicketsCommand` |
| Service method | `EndorsementSubmissionService.ResendApplicationFormsAsync` (`:538`) | `OMSTicketingMonitoringService.RetryTicketsAsync` (`:195`) |
| Repository method | `ATSRepository.RequeueEmailInvitationsAsync` (`ATSRepository.EmailInvitations.cs:108`) | `OMSTicketingRepository.RequeueExhaustedTicketsAsync` (`:277`) |
| Cap constant | `EndorsementSubmissionService.MaxBulkResendSize = 500` (`:9`) | `OMSTicketingMonitoringService.MaxBulkRetrySize = 500` (`:8`) |
| Browser surface | `Component/ATS/BulkUploads/BulkUploadSubjectsDialog.razor{,.cs,.css}` | `Component/ATS/OMSTicketing/TicketingStatusComponent.razor{,.cs}` |
| Queue it feeds | the email job, via `EmailSentStatus = Pending` | the ticketing job, via `TicketStatus = Pending` |
| Response DTO | `ResendApplicationFormsEndpointResponse` | `RetryTicketsEndpointResponse` |

Both endpoints return the same three numbers, both carry `ProducesProblem` for 400/403/404 and
**neither declares 409** — the single-row siblings do, and the difference is the whole point of
the design (a stale selection is reported in the counts, not thrown).

§2 traces the Bulk Uploads request end to end. §3 gives the ticketing one as a diff.

---

## 2. One request end to end: `PATCH /ats/resendapplicationforms`

### 2.1 The checkbox — `BulkUploadSubjectsDialog.razor`

The dialog's table declares `ColumnCount="7"` — six data columns plus a leading select column.
The header checkbox is disabled unless the current page has something selectable:

```razor
                <MudTh Class="ats-bulk-subjects-select-header">
                    @* Selects only the resendable rows on this page - checking a row the
                       server would refuse just produces a confusing "0 of N" result. *@
                    <MudCheckBox T="bool"
                                 Value="@AreAllSelectablesSelected"
                                 ValueChanged="@((bool value) => ToggleSelectAll(value))"
                                 Size="Size.Small"
                                 Disabled="@(!HasSelectableSubjects)"
                                 aria-label="Select all resendable subjects on this page" />
                </MudTh>
```

and per row, the checkbox is rendered **only** where `CanResend` is true — a non-resendable row
gets an empty cell, not a disabled checkbox:

```razor
                <MudTd DataLabel="Select" Class="ats-bulk-subjects-select-cell">
                    @if (CanResend(subject))
                    {
                        <MudCheckBox T="bool"
                                     Value="@_selectedInvitationIds.Contains(subject.EmailInvitationID)"
                                     ValueChanged="@((bool value) => ToggleSelection(subject.EmailInvitationID, value))"
                                     Size="Size.Small"
                                     aria-label="@($"Select {FormatName(subject)} for resending")" />
                    }
                </MudTd>
```

`CanResend` is the browser-side eligibility gate, and it is the same predicate that gates the
row's own single-resend button — so the two never disagree:

```csharp
	// Resend is offered only where it helps. A Pending or Processing invitation is
	// already in the email job's queue, so resending would double-send; a completed
	// form has nothing left to fill in.
	private static bool CanResend(BulkUploadSubjectListDTO subject)
	{
		if (string.Equals(
			subject.ApplicationFormStatus,
			SubjectApplicationFormStatus.Done,
			StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Error,
			StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// Delivered but not acted on, or withdrawn: a nudge is legitimate.
		return string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Done,
			StringComparison.OrdinalIgnoreCase);
	}
```

`BulkUploadSubjectsDialog.razor.cs:491-513`. Read that last branch carefully — it is **C2**, and
§8.1 explains what the server does with it.

### 2.2 The selection bar — its own band, above the table

Rendered only when something is selected, between the filter row and `<TableComponent>`:

```razor
        @if (_selectedInvitationIds.Count > 0)
        {
            <div class="ats-bulk-subjects-selection-bar" role="region" aria-live="polite">
                <span class="ats-bulk-subjects-selection-count">
                    @_selectedInvitationIds.Count selected
                </span>

                <div class="ats-bulk-subjects-selection-actions">
                    <button type="button"
                            class="ats-bulk-subjects-selection-clear"
                            disabled="@_isBulkResending"
                            @onclick="ClearSelection">
                        Clear
                    </button>

                    <button type="button"
                            class="ats-management-button ats-bulk-subjects-requeue"
                            disabled="@_isBulkResending"
                            @onclick="ConfirmBulkResendAsync">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                            <path d="M20 11A8 8 0 1 0 12 20M20 5v6h-6" />
                        </svg>
                        @(_isBulkResending ? "Queueing..." : "Resend selected")
                    </button>
                </div>
            </div>
        }
```

`BulkUploadSubjectsDialog.razor:80-106`. Both buttons carry `disabled="@_isBulkResending"`, which
is the double-click guard. `_isBulkResending` is a plain `bool`, not a per-row id — unlike the
single-row `_resendingInvitationId`, a bulk action has no meaningful "which row".

### 2.3 `ConfirmBulkResendAsync` → `BulkResendAsync`

The confirm dialog (`BulkUploadSubjectsDialog.razor.cs:361`) is the shared
`Component/Generic/YesNoDialogComponent`, and the `(Func<Task<bool>>)` cast on
`ConfirmActionAsync` is load-bearing (without it the lambda is ambiguous and the file does not
compile):

```csharp
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Each subject gets a fresh application link, and their previous one stops "
					+ "working. They are sent at a steady rate, so a large batch can take "
					+ "several minutes to go out."
			},
			{ nameof(YesNoDialogComponent.ConfirmIcon), Icons.Material.Outlined.Refresh },
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)BulkResendAsync
			},
```

`BulkResendAsync` (`BulkUploadSubjectsDialog.razor.cs:435`) — note the copy **before** the
call, because the reload prunes the set:

```csharp
			// Copied before the call: the list is pruned when the table reloads, and the
			// response has to be compared against what was actually submitted.
			var requestedIds = _selectedInvitationIds.ToList();

			var response = await EndorsementSubmissionService.ResendApplicationFormsAsync(requestedIds);
```

and the honest partial result at the end:

```csharp
			// A shortfall is normal rather than an error: the email job may have picked up
			// some of the selection between rendering and clicking. Saying so is more use
			// than a flat "done".
			if (result.IsComplete)
			{
				Snackbar.Add(
					$"{result.RequeuedCount} application form(s) queued for resending.",
					Severity.Success);
			}
			else
			{
				Snackbar.Add(
					$"{result.RequeuedCount} of {result.RequestedCount} application form(s) queued. "
						+ "The rest were already being sent.",
					Severity.Info);
			}
```

`Severity.Info`, not `Error` — the shortfall is a normal outcome. The selection is cleared
(`_selectedInvitationIds.Clear()`) and the table reloaded **before** the snackbar, so the operator
sees the rows move to `Pending` under the message.

### 2.4 UI service — `UI/FrontendWebassembly/Services/ATS/EndorsementSubmission/EndorsementSubmissionService.cs:251`

**Same class name as the backend service, different assembly, entirely different thing** — this
one is an `HttpClient` wrapper. Grepping `EndorsementSubmissionService` returns both.

```csharp
	public async Task<ServiceResponse<BulkRetryResultDTO>> ResendApplicationFormsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds)
	{
		var request = new { emailInvitationIds };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/resendapplicationforms", request);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<BulkRetryResultDTO>.Failure(await response.ReadErrorDetailAsync());
			}

			// The counts, not a bool: some of the selection may already be mid-send, and
			// the caller has to be able to report "3 of 5".
			var result = await response.Content.ReadFromJsonAsync<BulkRetryResultDTO>();

			return result is null
				? ServiceResponse<BulkRetryResultDTO>.Failure("The server returned an empty response.")
				: ServiceResponse<BulkRetryResultDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<BulkRetryResultDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
```

The URL is relative to the gateway with **no leading slash**. `ReadErrorDetailAsync()` is the only
route by which the server's `detail` (the cap message, the "nothing in scope" message) reaches the
snackbar.

The UI-side DTO is a hand-written mirror, and `IsComplete` is a **settable field** here rather
than the backend's computed property — JSON binding supplies it:

```csharp
public class BulkRetryResultDTO
{
	public int RequestedCount { get; set; }

	public int RequeuedCount { get; set; }

	public bool IsComplete { get; set; }
}
```

`UI/FrontendWebassembly/DTO/ATS/BulkRetryResultDTO.cs`. Registered
`services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();`
(`ServiceConfig/FrontendServiceConfig.cs:79`); `IBulkUploadService` at `:83` and
`IOMSTicketingService` at `:85`.

### 2.5 Gateway route — `BackendAPI/Modules/ATS/Path/ATSPaths.cs:614-622`

```csharp
			new RouteDefinitionDTO(
				RouteId: "ResendApplicationForms",
				MatchPath: "/ats/resendapplicationforms",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/resendapplicationforms" }
				}
			),
```

It sits immediately after the single-row `ResendApplicationForm` route (`:603-611`). Note the
singular/plural pair: `/ats/resendapplicationform` and `/ats/resendapplicationforms` are two
distinct `MatchPath` strings one line apart in the same collection, differing by one character.
**No `RateLimitPolicy` metadata**, so it falls through to the gateway default.

### 2.6 Carter endpoint — `Features/Web/ResendApplicationForms/ResendApplicationFormsEndpoint.cs`

The whole file:

```csharp
namespace ATS.Features.Web.ResendApplicationForms;

public record ResendApplicationFormsEndpointRequest(IReadOnlyCollection<Guid> EmailInvitationIds);

public record ResendApplicationFormsEndpointResponse(
	int RequestedCount,
	int RequeuedCount,
	bool IsComplete);

public class ResendApplicationFormsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("resendapplicationforms", async (
			ResendApplicationFormsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new ResendApplicationFormsCommand(request.EmailInvitationIds ?? []);

			var result = await sender.Send(command, cancellationToken);

			var response = new ResendApplicationFormsEndpointResponse(
				result.RequestedCount,
				result.RequeuedCount,
				result.IsComplete);

			// The full result, not a bool: a selection made a minute ago can contain
			// invitations the email job has since picked up, so "3 of 5" is a normal
			// outcome the operator has to be able to see.
			return Results.Ok(response);
		})
		.WithName("ResendApplicationForms")
		.WithTags("ATS")
		.Produces<ResendApplicationFormsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Resend Application Forms")
		.WithDescription(
			"Queues a fresh application form for every selected invitation, each with its "
			+ "own new token and a reset send-attempt budget. Invitations outside the "
			+ "caller's scope are ignored, and any the email job is actively sending are "
			+ "skipped, so the response reports how many of the requested invitations "
			+ "actually moved. Returns 400 above the batch limit and 404 when nothing in "
			+ "the selection is available to the caller.")
		.RequireAuthorization();
	}
}
```

`request.EmailInvitationIds ?? []` null-guards the collection before the command is built, so a
`{}` body reaches the validator as an empty collection and produces a 400 rather than an NRE.
The declared problem set is 400/403/404 — **no 409**.

### 2.7 Command, validator, handler — `ResendApplicationFormsHandler.cs`

```csharp
public record ResendApplicationFormsCommand(IReadOnlyCollection<Guid> EmailInvitationIds)
	: ICommand<ResendApplicationFormsResult>;

public record ResendApplicationFormsResult(int RequestedCount, int RequeuedCount, bool IsComplete);

public class ResendApplicationFormsCommandValidator : AbstractValidator<ResendApplicationFormsCommand>
{
	public ResendApplicationFormsCommandValidator()
	{
		RuleFor(x => x.EmailInvitationIds)
			.NotNull()
			.WithMessage("At least one invitation is required.")
			.Must(ids => ids is { Count: > 0 })
			.WithMessage("At least one invitation is required.");

		// The cap is enforced in the service too, because that is where the reason for it
		// lives. Validating here turns an oversized request into a 400 before it reaches a
		// database round trip.
		RuleFor(x => x.EmailInvitationIds)
			.Must(ids => ids is null || ids.Count <= EndorsementSubmissionService.MaxBulkResendSize)
			.WithMessage(
				$"A bulk resend is limited to {EndorsementSubmissionService.MaxBulkResendSize} invitations at a time.");

		RuleForEach(x => x.EmailInvitationIds)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}
```

The validator reads the **raw** collection's `Count`; the service caps the **deduplicated** count
(§2.8). The validator is therefore the stricter of the two, which is the safe direction — a
request of 600 ids with 400 distinct ones is still a 400.

The handler is a pass-through that reshapes the DTO:

```csharp
	public async Task<ResendApplicationFormsResult> Handle(
		ResendApplicationFormsCommand request,
		CancellationToken cancellationToken)
	{
		var result = await _endorsementSubmissionService.ResendApplicationFormsAsync(
			request.EmailInvitationIds,
			cancellationToken);

		return new ResendApplicationFormsResult(
			result.RequestedCount,
			result.RequeuedCount,
			result.IsComplete);
	}
```

### 2.8 The service — `Services/EndorsementSubmission/EndorsementSubmissionService.cs:538-643`

The cap and its reason, at the top of the class:

```csharp
	// A bulk resend is bounded because every requeued invitation becomes a message on the
	// deliberately-paced email queue. At the default 0.9 sends/second, 500 invitations is
	// already about nine minutes of sending; releasing thousands at once would block every
	// other client behind one operator's click.
	public const int MaxBulkResendSize = 500;
```

Then the method, in the order it actually runs:

```csharp
		// Distinct because a selection can repeat an id, and a duplicate would be counted
		// twice in the total reported back.
		var requestedIds = emailInvitationIds.Distinct().ToList();

		if (requestedIds.Count > MaxBulkResendSize)
		{
			throw new BadRequestException(
				$"A bulk resend is limited to {MaxBulkResendSize} invitations at a time. Narrow the selection and try again.");
		}

		var scope = await _accessScopeResolver.ResolveAsync(cancellationToken);

		if (scope is not { } accessScope)
		{
			throw new ForbiddenException("The current user does not have ATS access.");
		}

		var owners = await _atsRepository.GetEmailInvitationOwnersAsync(requestedIds, cancellationToken);
```

**`.Distinct()` before the cap check** (C9). `requestedIds.Count` — the deduplicated count — is
what `RequestedCount` reports at the end.

The per-row scope filter:

```csharp
		// Scope is enforced per row, not once for the request: without this a caller could
		// touch another client's invitations by posting their ids alongside their own.
		// Out-of-scope ids are dropped silently, for the same reason the single resend
		// answers 404 - naming them would confirm the invitations exist.
		var inScopeIds = owners
			.Where(owner => IsOwnerWithinScope(owner, accessScope))
			.Select(owner => owner.EmailInvitationID)
			.ToList();

		if (inScopeIds.Count == 0)
		{
			throw new NotFoundException("None of the selected invitations are available to resend.");
		}
```

An id that does not exist is simply absent from `owners`, so it never reaches `inScopeIds` and is
counted in neither number's numerator — it inflates `RequestedCount` and depresses
`RequeuedCount`, which reads to the operator as "already being sent". Slightly misleading, but
harmless: the UI prunes to visible rows, so a nonexistent id can only arrive from a hand-built
request.

The scope predicate — an identity-only projection check, so a 500-row batch never loads a whole
invitation row:

```csharp
	// The scope rule applied to an identity-only projection, so a bulk action can filter
	// many rows without loading each whole invitation.
	private static bool IsOwnerWithinScope(EmailInvitationOwnerDTO owner, AtsAccessScope scope)
	{
		if (scope.AuthorizedClientIds is { } clientIds
			&& !(owner.ClientId.HasValue && clientIds.Contains(owner.ClientId.Value)))
		{
			return false;
		}

		return !scope.RequiredOwnerId.HasValue
			|| owner.RequestorId == scope.RequiredOwnerId.Value;
	}
```

`:648-660`. `AuthorizedClientIds == null` means super admin; an **empty** collection means
"no client", which filters everything out — the `AtsAccessScope` doc comment is explicit that
empty is not null:

```csharp
/// <param name="AuthorizedClientIds">
/// null means every client (platform super admin). An empty collection means no client,
/// which filters everything out - empty is not the same as null.
/// </param>
public readonly record struct AtsAccessScope(
	IReadOnlyCollection<int>? AuthorizedClientIds,
	Guid? RequiredOwnerId);
```

`Services/AccessScope/IAtsAccessScopeResolver.cs`.

Then the per-row token generation, which is why this path cannot be one `UPDATE`:

```csharp
		// Each invitation gets its OWN token. Reusing one across the batch would let any
		// candidate in it open another candidate's application form.
		var requeues = new List<EmailInvitationRequeueDTO>(inScopeIds.Count);
		var newExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);

		foreach (var invitationId in inScopeIds)
		{
			var token = _secureToken.GenerateSecureToken();

			if (string.IsNullOrEmpty(token))
			{
				_logger.LogError("Failed to generate new token during bulk resend: {@Context}", logContext);
				throw new InternalServerException("Failed to generate new token.");
			}

			var hashToken = _hashService.Hash(token);

			if (string.IsNullOrEmpty(hashToken))
			{
				_logger.LogError("Failed to hash token during bulk resend: {@Context}", logContext);
				throw new InternalServerException("Failed to hash token.");
			}

			requeues.Add(new EmailInvitationRequeueDTO
			{
				EmailInvitationId = invitationId,
				HashToken = hashToken,
				HashTokenExpiration = newExpiration
			});
		}

		var requeued = await _atsRepository.RequeueEmailInvitationsAsync(requeues, cancellationToken);
```

One shared `newExpiration` for the whole batch but a **distinct token per row**. A
token-generation failure at row *n* throws out of the method — see §8.2 for what that leaves
behind.

`EmailInvitationRequeueDTO` exists precisely to carry the token alongside the id, and its doc
comment records who is responsible for generating it:

```csharp
/// The token travels with the id because every requeued invitation needs its OWN freshly
/// generated one - reusing a single token across a batch would let any candidate in it open
/// another candidate's application form. Generating them is the service's job (it owns
/// <c>ISecureToken</c> and <c>IHashService</c>), so the repository is handed the finished
/// values rather than reaching for those itself.
public sealed class EmailInvitationRequeueDTO
{
	public Guid EmailInvitationId { get; set; }

	public string HashToken { get; set; } = string.Empty;

	public DateTime HashTokenExpiration { get; set; }
}
```

Finally the honest count:

```csharp
		return new BulkRetryResultDTO
		{
			RequestedCount = requestedIds.Count,
			RequeuedCount = requeued
		};
```

`RequestedCount` is what the caller asked for (deduplicated); `RequeuedCount` is what the
database says actually moved. The service **never** claims success it did not achieve — this is
the correctness property the design doc is most concerned with, and it holds.

### 2.9 The cache decorator — `Data/Cache/EmailInvitations/ATSCacheRepository.EmailInvitations.Cache.cs:121`

`IATSRepository` is registered then decorated (`ATSServiceConfiguration.cs:47-48`), and
`IEmailInvitationRepository` forwards to the decorated aggregate (`:56`), so **this path goes
through the cache decorator**:

```csharp
	public async Task<int> RequeueEmailInvitationsAsync(
		IReadOnlyCollection<EmailInvitationRequeueDTO> requeues,
		CancellationToken cancellationToken)
	{
		var requeued = await _atsRepository.RequeueEmailInvitationsAsync(requeues, cancellationToken);

		if (requeued > 0)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.Report, cancellationToken);
			await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication, cancellationToken);
		}

		return requeued;
	}
```

Revocation is conditional on `requeued > 0` — a batch where every row was mid-send leaves the
cache alone. **C5: the design doc's file list does not mention this file.**

The scope read beside it is deliberately *not* cached, with the reason inline:

```csharp
	// Scope identity read on the way into a write - not worth a cache entry, and stale
	// scope data here would be a correctness problem. Same reasoning as the single-row
	// GetEmailInvitationOwnerAsync above.
	public async Task<List<EmailInvitationOwnerDTO>> GetEmailInvitationOwnersAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationOwnersAsync(emailInvitationIds, cancellationToken);
	}
```

### 2.10 The repository — `Data/Repository/EmailInvitations/ATSRepository.EmailInvitations.cs:108`

```csharp
	public async Task<int> RequeueEmailInvitationsAsync(
		IReadOnlyCollection<EmailInvitationRequeueDTO> requeues,
		CancellationToken cancellationToken)
	{
		if (requeues.Count == 0)
		{
			return 0;
		}

		// One UPDATE per row rather than one for the set, because each invitation needs its
		// OWN freshly generated token - a shared token would let any candidate in the batch
		// open another candidate's form. They run inside the caller's transaction, so the
		// batch still commits or rolls back as a unit.
		//
		// The predicate matches RequeueEmailInvitationAsync: a row the job is actively
		// sending (Processing) is skipped rather than raced, and the returned count reflects
		// what actually moved.
		var requeued = 0;

		foreach (var requeue in requeues)
		{
			var updated = await _dbcontext.EmailInvitationRequests
				.Where(x => x.EmailInvitationID == requeue.EmailInvitationId
						 && x.EmailSentStatus != EmailStatus.Processing)
				.ExecuteUpdateAsync(setters => setters
					.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
					.SetProperty(x => x.EmailSendAttempts, x => 0)
					.SetProperty(x => x.EmailClaimedAt, x => null)
					.SetProperty(x => x.EmailSentAt, x => null)
					.SetProperty(x => x.HashToken, requeue.HashToken)
					.SetProperty(x => x.HashTokenCreatedAt, DateTime.UtcNow)
					.SetProperty(x => x.HashTokenExpiration, requeue.HashTokenExpiration)
					.SetProperty(x => x.OrderStatus, OrderStatus.PendingCandidateInfo)
					.SetProperty(x => x.ApplicationFormStatus, ApplicationFormStatus.Pending),
					cancellationToken);

			requeued += updated;
		}

		return requeued;
	}
```

Ten `SetProperty` calls per row. Three of them are not status bookkeeping — `OrderStatus` is reset
to `PendingCandidateInfo` and `ApplicationFormStatus` to `Pending`, and the token trio is
reissued. That is what makes a requeue visible to the candidate: the old link stops working and a
new one is on its way.

**The comment's second sentence is wrong** — see §8.2. There is no caller transaction.

The scope read, `AsNoTracking()` and identity-only:

```csharp
	public async Task<List<EmailInvitationOwnerDTO>> GetEmailInvitationOwnersAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		if (emailInvitationIds.Count == 0)
		{
			return [];
		}

		var ids = emailInvitationIds.ToList();

		// Read before the update so the caller's scope can be enforced per row. A bulk
		// action must not become a way to touch another client's invitations by posting
		// their ids alongside your own.
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => ids.Contains(eir.EmailInvitationID))
			.Select(eir => new EmailInvitationOwnerDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				ClientId = eir.ClientId,
				RequestorId = eir.RequestorId
			})
			.ToListAsync(cancellationToken);
	}
```

### 2.11 History — `Services/OrderHistory/OrderHistoryService.cs:17`

```csharp
	public Task RecordManyAsync(IReadOnlyCollection<Guid> invitationIds, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web, Guid? changedByUserId = null) =>
		_repository.AddRangeAsync(
			invitationIds.Select(id => _factory.Create(id, eventType, previousStatus, newStatus, source, changedByUserId)).ToList(),
			cancellationToken);
```

One `AddRangeAsync` for the whole set — a single `SaveChanges`, so the entries commit together.
`changedByUserId` defaults to null, which makes `OrderHistoryFactory.Create`
(`Services/OrderHistory/OrderHistoryFactory.cs`) fall back to the acting user:

```csharp
		// Background jobs run with no HttpContext, so ICurrentUser resolves to null
		// there. They pass the originating user explicitly instead.
		var userId = changedByUserId ?? _currentUser.UserId;
```

That resolves normally here because `EndorsementSubmissionService` is HTTP-scoped.

Quoted from the call site, since that is what this feature actually passes
(`EndorsementSubmissionService.cs:621-631`):

```csharp
		// Recorded for the rows the caller was allowed to act on. A row skipped because the
		// job is mid-send keeps its own history from that send, so no entry is lost.
		if (requeued > 0)
		{
			await _orderHistoryService.RecordManyAsync(
				inScopeIds,
				OrderHistoryEventType.ApplicationFormResent,
				null,
				OrderStatus.PendingCandidateInfo,
				cancellationToken);
		}
```

Event type `ApplicationFormResent`. `previousStatus` is `null` — the bulk path does not read each
row's prior order status, unlike the single-row path which passes `invitation.OrderStatus`.
`changedByUserId` is left to default, so `OrderHistoryFactory` stamps `ICurrentUser.UserId`; that
resolves normally because this service is HTTP-scoped. **The id set is `inScopeIds`, not the rows
that moved** — §8.3.

### 2.12 Back to the browser

`Results.Ok(response)` → `ReadFromJsonAsync<BulkRetryResultDTO>()` → `result.IsComplete` →
`Severity.Success` or `Severity.Info` (§2.3). The row reloads through
`BulkUploadService.GetSubjectsAsync` and now reads `Pending` with `EmailSendAttempts` cleared —
that visible reset is the operator's confirmation.

Closing the dialog then refreshes the board, because the rollup counts changed
(`BulkUploadsComponent.razor.cs:119-124`):

```csharp
		await dialog.Result;

		// Resending from the dialog changes a row's email rollup, so pick the new
		// figures up rather than leaving the dashboard showing pre-resend counts.
		await ReloadTableAsync();
```

---

## 3. The ticketing slice, as a diff from §2

Same shape end to end. Only what genuinely differs:

**Route and DTOs.** `app.MapPatch("retrytickets", …)`, `RetryTicketsEndpointRequest(IReadOnlyCollection<Guid> EmailInvitationIds)`,
`RetryTicketsEndpointResponse(int RequestedCount, int RequeuedCount, bool IsComplete)`. Identical
`ProducesProblem` set (400/403/404, no 409) and identical `?? []` null guard.

**Validator.** Character-for-character the ticketing twin of §2.7, swapping
`EndorsementSubmissionService.MaxBulkResendSize` for `OMSTicketingMonitoringService.MaxBulkRetrySize`
and "invitation" for "order". Same duplicated comment about why the cap appears in both places.

**Service — one extra read, and a different order of operations.**
`OMSTicketingMonitoringService.RetryTicketsAsync` (`:195-277`) runs: 403 check → `.Distinct()` →
cap → `GetRetryTargetsAsync` → per-row scope filter → 404 if empty → **`GetExhaustedTicketIdsAsync`**
→ `RequeueExhaustedTicketsAsync` → history. The extra read is **C6**, and the comment on the
repository method explains what it is for:

```csharp
		// Claimed before the update so the set is known: ExecuteUpdate returns a count, not
		// the rows it touched. Anything already requeued or re-claimed by the job simply
		// does not match the predicate.
		var eligibleIds = await _ticketingRepository.GetExhaustedTicketIdsAsync(
			inScopeIds,
			cancellationToken);
```

It exists because `ExecuteUpdateAsync` returns a count and nothing else, and the service wants to
record history only for rows it believes moved. §8.4 explains the gap that opens.

The email side has no equivalent read — it cannot have one, because the predicate is per-row.

**Scope predicate — duplicated logic, different syntax.** `IsWithinScope`
(`OMSTicketingMonitoringService.cs:279-291`) means the same as `IsOwnerWithinScope` but is written
differently and lives in a different service:

```csharp
	// Mirrors the read path's scope rule: a null client set means unrestricted (super
	// admin), and RequiredOwnerId restricts a user to the orders they raised.
	private static bool IsWithinScope(TicketRetryTargetDTO target, AtsAccessScope scope)
	{
		if (scope.AuthorizedClientIds is { } clientIds
			&& (target.ClientId is not { } clientId || !clientIds.Contains(clientId)))
		{
			return false;
		}

		return !scope.RequiredOwnerId.HasValue
			|| target.RequestorId == scope.RequiredOwnerId.Value;
	}
```

`target.ClientId is not { } clientId || !clientIds.Contains(clientId)` versus
`!(owner.ClientId.HasValue && clientIds.Contains(owner.ClientId.Value))`. Equivalent by De Morgan,
but nothing enforces that they stay equivalent. The ticketing one is *also* used by the single-row
`RetryTicketAsync`; the email one is not (that path uses `IsInvitationWithinCallerScopeAsync`,
which loads the whole invitation).

**History.** Same `RecordManyAsync`, different event type and different id set:

```csharp
		// Recorded only for orders that were eligible, so a stale id in the selection never
		// produces a history entry claiming a retry that did not happen. The order's own
		// status is unchanged - this is a ticketing action, not a lifecycle step.
		if (eligibleIds.Count > 0)
		{
			await _orderHistoryService.RecordManyAsync(
				eligibleIds,
				OrderHistoryEventType.TicketRetryRequested,
				null,
				string.Empty,
				cancellationToken);
		}
```

`OrderHistoryEventType.TicketRetryRequested`, `previousStatus: null`, `newStatus: string.Empty` —
a ticket retry is not an order-lifecycle step, so the order's own status must not appear to
change. Contrast the email side, which passes `OrderStatus.PendingCandidateInfo` because a resend
*genuinely* resets the order status (§2.10).

**Cache.** `IOMSTicketingRepository` is registered **directly**
(`services.AddScoped<IOMSTicketingRepository, OMSTicketingRepository>();`,
`ATSServiceConfiguration.cs:71`), bypassing the `ATSCacheRepository` decorator entirely — so no
tag revocation happens on this path. That is deliberate for the read side (`TicketStatus` moves
inside a single 10-second tick) and is documented in
[`oms-auto-ticketing_code_explanation.md`](../oms-auto-ticketing/oms-auto-ticketing_code_explanation.md)
§8.1. The consequence for *this* feature is an asymmetry: a bulk resend invalidates two cache
tags, a bulk ticket retry invalidates none.

**UI.** Covered in full by the OMS doc §6.4. The selection bar markup is
`TicketingStatusComponent.razor:59-85` and uses the shared `.ats-status-board-selection-*`
classes; `ColumnCount="8"` (seven data columns plus select); the pruning line is
`TicketingStatusComponent.razor.cs:125`; `BulkRetryAsync` is at `:377-431`. Its shortfall message
reads *"The rest were already back in the queue."* where the dialog reads *"The rest were already
being sent."*

---

## 4. The two requeue predicates, side by side

This is the most important code in the feature, and the design doc gets it wrong (**C1**).

### 4.1 Ticketing — `OMSTicketingRepository.cs:277`

```csharp
	public async Task<int> RequeueExhaustedTicketsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		if (emailInvitationIds.Count == 0)
		{
			return 0;
		}

		var ids = emailInvitationIds.ToList();

		// The set version of RequeueExhaustedTicketAsync, and deliberately the same
		// predicate: ids the caller sent that are no longer exhausted simply do not match,
		// so a stale selection requeues the rows that are still valid instead of failing
		// the whole request. The returned count is what actually changed, which is what the
		// operator is told.
		return await _dbContext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID)
					 && !x.IsTicketed
					 && x.TicketStatus == TicketStatus.Error
					 && x.TicketAttempts >= MaxTicketAttempts)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.TicketStatus, x => TicketStatus.Pending)
				.SetProperty(x => x.TicketAttempts, x => 0)
				.SetProperty(x => x.TicketError, x => (string?)null)
				.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null),
				cancellationToken);
	}
```

Compare with the single-row `RequeueExhaustedTicketAsync` (`:249-275`) — **identical predicate,
identical setters**, only `x.EmailInvitationID == emailInvitationId` becoming
`ids.Contains(x.EmailInvitationID)` and the return becoming a count instead of a bool:

```csharp
		// The predicate is the concurrency guard, not just a lookup: matching on the
		// exhausted state inside the UPDATE means a row the job has already re-claimed,
		// or that another operator retried a moment earlier, updates nothing and the
		// caller is told so. A read-then-write would race and could resurrect a live
		// claim. IsTicketed is checked too - a ticketed order must never re-enter the
		// queue and raise a second ticket in OMS.
		var updated = await _dbContext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationId
					 && !x.IsTicketed
					 && x.TicketStatus == TicketStatus.Error
					 && x.TicketAttempts >= MaxTicketAttempts)
```

**They follow the same "match the exhausted state inside the UPDATE" pattern.** One statement, so
the batch is atomic. `!x.IsTicketed` is the guard that prevents a **duplicate OMS ticket**.
Resetting `TicketAttempts` to 0 gives a fresh budget of five automatic attempts, and the predicate
then no longer matches — which is what makes a second concurrent call return 0.

### 4.2 Email — `ATSRepository.EmailInvitations.cs:108`

Quoted in full at §2.10. The predicate, isolated:

```csharp
				.Where(x => x.EmailInvitationID == requeue.EmailInvitationId
						 && x.EmailSentStatus != EmailStatus.Processing)
```

**They diverge.** Three differences, all consequential:

| | Ticketing | Email |
|---|---|---|
| Statements | **one** `ExecuteUpdateAsync` for the whole set | **one per row**, in a `foreach` |
| Guard form | positive: `TicketStatus == Error && TicketAttempts >= MaxTicketAttempts` | negative: `EmailSentStatus != Processing` |
| Terminal-state guard | `!x.IsTicketed` | **none** |
| Atomic? | yes | no (§8.2) |
| Self-invalidating? | yes — after the update the row is `Pending` with 0 attempts, so it stops matching | **no** — after the update the row is `Pending`, which is still `!= Processing`, so it matches again (§8.5) |

`EmailStatus` has four values (`BackendAPI/Modules/ATS/Constants/EmailStatus.cs`, `internal static
class`):

```csharp
	internal const string Pending = "Pending";
	internal const string Processing = "Processing";
	internal const string Done = "Done";
	internal const string Error = "Error";
```

So the email predicate admits **three of the four** states: `Pending`, `Done` and `Error`. Only
`Processing` — the job's in-memory claim — is excluded. The single-row sibling documents why, and
the reasoning is sound for two of the three:

```csharp
		// Processing is the one status excluded. That row is mid-send RIGHT NOW - a worker
		// is holding it in memory and is about to write its outcome, so re-issuing the token
		// here would both race that write and risk a second delivery.
		//
		// Pending is allowed even though the row is already queued: nothing has been sent,
		// so re-issuing the token duplicates no email, and refusing would give an operator a
		// confusing error for clicking resend twice.
```

`ATSRepository.EmailInvitations.cs:70-81`. Nothing in either comment addresses `Done`, which is
the state where a second email genuinely goes out.

### 4.3 Why the divergence is not accidental

The two queues have different terminal semantics. `IsTicketed` is a real terminal flag: once OMS
has returned a ticket number, re-queueing would raise a **second ticket for the same order**, which
is unrecoverable. Email has no equivalent — a delivered invitation is not "finished", because the
subject may not have acted on it, and re-inviting is a legitimate business action (that is exactly
what `CanResend`'s `Done` branch encodes).

So the absence of a terminal guard on the email side is a defensible design choice. What is *not*
defensible is that the one rule the browser does enforce against `Done` — `ApplicationFormStatus
== Done` means "this subject already completed the form, do not re-invite them" — has no
server-side counterpart at all. That is §8.1.

---

## 5. Selection state in the browser

Both surfaces store selection the same way, and both are **page-scoped**, not global.

### 5.1 Storage

```csharp
	// Ids rather than rows, so a selection survives the list reloading underneath it. It is
	// cleared whenever the filter or search changes, because a selection the user can no
	// longer see is one they cannot reason about.
	private readonly HashSet<Guid> _selectedInvitationIds = [];
```

`BulkUploadSubjectsDialog.razor.cs:37-40`. `TicketingStatusComponent.razor.cs:43` declares the
identical field. Ids, not DTOs, so a reload replacing the row objects does not orphan the
selection.

### 5.2 What "all" means

`CursorTableLoader` tracks cursors and counts, not items, so each component keeps its own copy of
the current page:

```csharp
	// The rows on the page right now. Held here because CursorTableLoader tracks cursors
	// and counts, not the items themselves, and the select-all checkbox has to know what
	// "all" currently means.
	private IReadOnlyList<BulkUploadSubjectListDTO> _pageSubjects = [];

	// The rows currently rendered that a resend actually applies to. Selecting a row the
	// server would refuse only produces a confusing "0 of N" outcome.
	private IEnumerable<BulkUploadSubjectListDTO> SelectableSubjects =>
		_pageSubjects.Where(CanResend);

	private bool HasSelectableSubjects => SelectableSubjects.Any();

	private bool AreAllSelectablesSelected =>
		HasSelectableSubjects
		&& SelectableSubjects.All(subject => _selectedInvitationIds.Contains(subject.EmailInvitationID));
```

`BulkUploadSubjectsDialog.razor.cs:44-61`; the ticketing twin is
`TicketingStatusComponent.razor.cs:50-61` with `SelectableOrders => _pageOrders.Where(CanRetry)`.
`AreAllSelectablesSelected` is an `All` over the **selectable** rows, not the page, so a page
mixing retryable and non-retryable rows still shows a checked header once every retryable one is
ticked.

### 5.3 The pruning line

This is the line the design doc's §4 rests on. Verbatim, from `LoadSubjectsAsync`:

```csharp
		// Recorded so the select-all checkbox knows what is on screen, and so a selection
		// can be pruned to it below.
		_pageSubjects = tableData.Items?.ToList() ?? [];

		// A row that left the page - filtered out, paged past, or no longer resendable
		// after a reload - drops out of the selection. Keeping it would let an operator
		// submit ids they can no longer see.
		if (_selectedInvitationIds.Count > 0)
		{
			var stillSelectable = SelectableSubjects
				.Select(subject => subject.EmailInvitationID)
				.ToHashSet();

			_selectedInvitationIds.RemoveWhere(id => !stillSelectable.Contains(id));
		}
```

`BulkUploadSubjectsDialog.razor.cs:97-111`. The pruning key is:

```csharp
			_selectedInvitationIds.RemoveWhere(id => !stillSelectable.Contains(id));
```

— line **110**. `TicketingStatusComponent.razor.cs:125` has the identical line with
`SelectableOrders`. The design doc's claim is **verified**: selection is page-scoped, and any id
that is no longer on screen *or no longer eligible* is dropped on every reload.

Two consequences worth knowing before changing this:

- The prune runs inside the `LoadServerData` callback, so it fires on paging, on filter change, on
  search, and on the explicit reload after a bulk action. There is no path that renders the table
  without pruning.
- Because `stillSelectable` is filtered by `CanResend`/`CanRetry`, a row that *becomes* ineligible
  while selected (its status moved server-side) drops out silently. The operator's count decreases
  with no explanation — acceptable, since the alternative is submitting a row the server would
  refuse.

### 5.4 Select-all is page-scoped on purpose

```csharp
	// Scoped to the current page on purpose. Selecting rows the operator has not seen -
	// across pages or the whole filter - is how a click ends up resending far more than
	// intended.
	private void ToggleSelectAll(bool isSelected)
	{
		foreach (var subject in SelectableSubjects)
		{
			ToggleSelection(subject.EmailInvitationID, isSelected);
		}
	}

	// The selection bar's own escape hatch. Unchecking rows one at a time is the only
	// other way out, which is tedious once a whole page is selected.
	private void ClearSelection() => _selectedInvitationIds.Clear();
```

`BulkUploadSubjectsDialog.razor.cs:346-359`. `ToggleSelectAll` iterates `SelectableSubjects`,
which is derived from `_pageSubjects` — so with `RowsPerPage="10"` the practical maximum from one
click is 10, and reaching the 500 cap requires 50 deliberate page-and-select cycles. The cap is a
backstop against a hand-built request, not against the UI.

---

## 6. The selection-bar CSS: shared on one board, restated on the other

**The two surfaces do not share these rules.** The Ticketing board uses the shared
`.ats-status-board-selection-*` classes from `wwwroot/css/ats.css`; the Bulk Uploads dialog
re-declares equivalents locally as `.ats-bulk-subjects-selection-*`. `BulkUploadsComponent.razor.css`
contains **no** selection or requeue rule at all — the board has no selection bar.

### 6.1 The shared rules — `wwwroot/css/ats.css`

All anchored on `.ats-management-page`, the bar itself opened by the comment immediately above
`:842`:

```css
/* The selection bar: its own band between the filter chips and the table, not a control
   inside the table toolbar.

   It lives here because sharing the toolbar row forced the button to align with the
   search box and the reload button, and pushed both out of shape the moment a selection
   existed. As a separate band it can appear, grow and disappear without moving anything
   except the table below it. */
.ats-management-page .ats-status-board-selection-bar {
```

| Selector | Line | What it carries |
|---|---|---|
| `.ats-status-board-select-header`, `-select-cell` | 790 | `width: 48px`, centred, `padding-right: 0` |
| `.ats-status-board-selection-bar` | 842 | flex row, `margin: 12px 0 16px`, `position: sticky; top: 0; z-index: 3`, opaque layered background |
| `.ats-status-board-selection-count` | 881 | `font-size: 13.5px; font-weight: 700` |
| `.ats-status-board-selection-actions` | 887 | flex row, `gap: 8px`, `flex-wrap: wrap` |
| `.ats-status-board-selection-clear` (+ `:hover:not(:disabled)`, `:disabled`) | 895, 906, 911 | quiet secondary button |
| `.ats-status-board-requeue` (+ `:disabled`, `:disabled:active`) | 918, 923, 929 | `flex: 0 0 auto; white-space: nowrap` |
| all of the above, stacked | 1058-1073 (inside `@media (max-width: 600px)` at 1032) | `position: static`, column layout, full-width buttons |

The two properties the design doc warns about are both present and both commented:

```css
    /* Reads as a live state rather than another filter: tinted with the same accent the
       checkboxes use, so it is obviously about the selection.
       Layered over an OPAQUE surface - the tint alone is translucent, and table rows
       would show through it while scrolling. */
    border: 1px solid var(--management-blue-600);
    border-radius: 12px;
    background:
        linear-gradient(
            color-mix(in srgb, var(--management-blue-600) 10%, transparent),
            color-mix(in srgb, var(--management-blue-600) 10%, transparent)),
        var(--c-surface);
```

and

```css
       0 works as the offset because the console topbar is not sticky - nothing overlaps
       this. Sticky also needs no `overflow` on any ancestor, which holds here:
       .ats-console, .ats-console-main and .ats-console-content all leave it unset. */
    position: sticky;
    top: 0;
    z-index: 3;
```

Note the accent token differs between the two copies: the shared rule uses
`var(--management-blue-600)` / `var(--management-text-1)` / `var(--management-border-strong)`,
the dialog's uses a locally-aliased `var(--blue-600)` / `var(--text-1)` / `var(--border)`.

### 6.2 The restated copy — `BulkUploadSubjectsDialog.razor.css`

The dialog declares its own palette aliases first, and the selection bar is one of the four
selectors that must be listed to receive them:

```css
/* Local palette, matching the block BulkUploadsComponent.razor.css declares. The
   drill-down is the same module and deliberately does not introduce a new hue.

   Every element that reads one of these aliases must be listed here - they are scoped
   to these selectors, not inherited from the dialog root, so a new block that uses
   var(--blue-600) without being added resolves to nothing and renders unstyled. */
.ats-bulk-subjects-header,
.ats-bulk-subjects-segmented,
.ats-bulk-subjects-selection-bar,
::deep .ats-bulk-subjects-table-card {
```

`:1-21`. Then the bar itself, at `:128-170`, opened by the comment that carries **C4**:

```css
/* The selection bar: its own band between the filter row and the table, mirroring
   .ats-status-board-selection-bar on the Ticketing board. Restated rather than shared
   because this dialog is not rendered inside .ats-management-page and so cannot match
   that rule's page anchor - keep the two in step.

   It sits outside the filter row on purpose: sharing that row meant the button had to
   align with the export control, and it moved the tabs every time a selection changed. */
.ats-bulk-subjects-selection-bar {
```

That reason does not hold. `BulkUploadSubjectsDialog.razor:17` renders:

```razor
      <div class="ats-management-page ats-bulk-subjects-shell ats-dialog-shell">
```

so the dialog *is* inside `.ats-management-page`, and `ats.css` is a global stylesheet, not a
scoped one — a global `.ats-management-page .ats-status-board-selection-bar` rule would match an
element carrying that class here. The duplication may still be justified (the dialog's sticky
offset pins to `.ats-dialog-body`, the accent tokens differ, the margins differ: `12px 24px 16px`
versus `12px 0 16px`) but the recorded reason is wrong, and it has been copied verbatim into two
places in the CSS and once into the design doc.

The one substantive difference is the sticky container, documented inline:

```css
    /* Follows the list down, matching the Ticketing board. The scroll container here is
       .ats-dialog-body rather than the page, so `top: 0` pins it to the top of the
       dialog's own scroll area - just under the gradient header. */
    position: sticky;
    top: 0;
    z-index: 3;
```

`.ats-scroll-dialog .ats-dialog-body` (`ats.css:1931-1940`) is that container, with
`overflow-y: auto !important`. And the divergence (**C3**):

```css
@media (max-width: 720px) {
```

`:445`, containing at `:462`:

```css
    /* Stops being sticky at this width, matching the Ticketing board: stacked it is
       roughly two rows tall, which on a phone would cover a meaningful share of the
       visible list for the whole scroll. */
    .ats-bulk-subjects-selection-bar {
        position: static;
        flex-direction: column;
        align-items: stretch;
        margin: 12px 16px 16px;
        box-shadow: none;
    }
```

The comment says "matching the Ticketing board" inside a block that does not match it — 720px
versus 600px. Between 601px and 720px the two bars behave differently.

The dialog also restates the checkbox gutter, with the same incorrect reason:

```css
/* The same 48px gutter as .ats-status-board-select-* in ats.css, restated because this
   dialog is not rendered inside .ats-management-page and so cannot match that rule's
   page anchor. Keep the two in step: they are the same control on two screens. */
::deep .ats-bulk-subjects-select-header,
::deep .ats-bulk-subjects-select-cell {
    padding-right: 0;
    text-align: center;
    width: 48px;
}
```

`:429-438`. Note the `::deep` — MudBlazor renders `MudTh`/`MudTd` outside the component's own
scope attribute, so the dialog's scoped sheet needs it. The shared `ats.css` rule does not, being
global.

---

## 7. Tests

### 7.1 What exists

`Test/Test/BackendAPI/Modules/ATS.IntegrationTests/ResendApplicationFormIntegrationTests.cs`,
`#region Bulk` at `:560`:

| Test | Line | Pins |
|---|---|---|
| `ResendApplicationForms_ShouldRequeueEveryInvitation_WithItsOwnToken` | 563 | `RequestedCount == 2`, `RequeuedCount == 2`, `IsComplete` true, all rows `Pending` with `EmailSendAttempts == 0`, and `saved.Select(x => x.HashToken).Should().OnlyHaveUniqueItems()` |
| `ResendApplicationForms_ShouldSkipInvitationsThatAreMidSend` | 604 | `RequeuedCount == 1` of 2, `IsComplete` **false**, and the `Processing` row keeps its original token — the in-flight send is not disturbed |
| `ResendApplicationForms_ShouldIgnoreInvitationsOutsideTheCallerScope` | 643 | posting `ClientB`'s id alongside your own: `RequeuedCount == 1`, the other row's token and `Error` status untouched |
| `ResendApplicationForms_ShouldThrowNotFound_WhenNothingIsInScope` | 680 | `NotFoundException`, with the comment *"not a 403 - naming the invitation would confirm it exists"* |
| `ResendApplicationForms_ShouldRejectABatchOverTheLimit` | 704 | `MaxBulkResendSize + 1` random guids → throws. Note it asserts `ThrowAsync<Exception>()`, **not** `BadRequestException`, so it does not distinguish the validator's 400 from the service's |

`Test/Test/BackendAPI/Modules/ATS.IntegrationTests/OMSTicketingRepositoryIntegrationTests.cs`:

| Test | Line | Pins |
|---|---|---|
| `RequeueExhaustedTicketsAsync_ShouldRequeueEveryExhaustedOrder` | 323 | count 2, both `Pending` with `TicketAttempts == 0`, **and** `ClaimPendingTicketsAsync` then actually returns them — the end-to-end effect |
| `RequeueExhaustedTicketsAsync_ShouldSkipIneligibleOrders_WithoutFailingTheBatch` | 356 | three seeded rows (exhausted, `ticketAttempts: 2`, and exhausted-but-`isTicketed: true`) → `requeued.Should().Be(1)`, and the still-retrying row is asserted untouched |
| `RequeueExhaustedTicketsAsync_ShouldReturnZero_WhenNoIdsAreGiven` | 387 | the empty-collection short circuit |
| `GetRetryTargetsAsync_ShouldReturnScopeIdentityForEveryKnownOrder` | 395 | the scope read returns both seeded ids |
| `RequeueExhaustedTicketAsync_ShouldBeIdempotent_WhenCalledTwice` | 417 | **the racing-calls property — single row only** |

The racing test, verbatim:

```csharp
	[Fact]
	public async Task RequeueExhaustedTicketAsync_ShouldBeIdempotent_WhenCalledTwice()
	{
		var order = await SeedQueuedOrderAsync(
			TicketStatus.Error,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts);

		var first = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		var second = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		// Two operators clicking at once must not both succeed; the second sees the
		// row is already queued and reports it.
		first.Should().BeTrue();
		second.Should().BeFalse();
	}
```

### 7.2 What does not exist (**C11**)

- **No test calls `RetryTicketsAsync`** — the ticketing bulk *service* method. Grep for
  `RetryTicketsAsync` across `Test/` returns zero hits. `OMSTicketingMonitoringServiceTests.cs`
  has eight tests, every one on the single-row `RetryTicketAsync` (`:61, 73, 91, 103, 113, 135,
  151, 174`). So the ticketing side's cap check, `.Distinct()`, per-row scope filter,
  `GetExhaustedTicketIdsAsync` read, `RecordManyAsync` call and `NotFoundException` branch are
  all **untested**; only the repository predicate beneath them is covered.
- **No bulk racing test on either side.** Nothing calls `RequeueExhaustedTicketsAsync` or
  `RequeueEmailInvitationsAsync` twice with the same ids and asserts the second call moves
  nothing. §8.5 explains why the email side could not pass such a test as written.
- **No test asserts history entries for either bulk path.** `RetryTicketAsync_ShouldRecordWhoForcedTheRetry`
  covers the single-row case only.
- **No test asserts the batch is atomic** — because it is not (§8.2).

---

## 8. Sharp edges

Findings only; nothing here has been changed.

### 8.1 The email requeue has no terminal-state guard, and one of its three eligibility rules exists only in the browser

The predicate is `x.EmailSentStatus != EmailStatus.Processing` (§4.2). It never looks at
`ApplicationFormStatus`. Compare the browser's rule:

```csharp
		if (string.Equals(
			subject.ApplicationFormStatus,
			SubjectApplicationFormStatus.Done,
			StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
```

and the message it shows for that case:

```csharp
			SubjectApplicationFormStatus.Done
			? "This subject already completed their application form."
			: "The invitation email has not been sent yet. Resending would send it twice.";
```

`BulkUploadSubjectsDialog.razor.cs:493-499` and `:524-529`. **No server-side check corresponds to
either branch.** Post an id whose subject has already completed the form and the `UPDATE` matches:
`ApplicationFormStatus` is reset to `Pending`, `OrderStatus` to `PendingCandidateInfo`, a fresh
token is issued, the old link dies, and the email job sends an invitation to fill in a form that
is already submitted — up to 500 per request.

This is a **missing server-side counterpart to a client-side rule**, on an endpoint that takes
caller-supplied ids. It is not new with bulk requeue: the single-row `RequeueEmailInvitationAsync`
has the identical predicate, and `ResendApplicationForm_ShouldWorkMultipleTimesForSameRecord`
(`ResendApplicationFormIntegrationTests.cs:454`) seeds `EmailSentStatus = "Done"` and asserts two
consecutive resends both succeed with different tokens. Bulk multiplies the blast radius by 500
and reduces it to one click.

Related, and milder: a `Done` invitation requeued through the UI (the deliberate "nudge" of **C2**)
**does** send a second email to a candidate who already received one. That is intended. What is
not intended is that there is no cap on how many nudges, and each one invalidates the previous
link — so an operator clicking resend twice in a row can leave a candidate holding a dead link.

The ticketing side has no equivalent exposure: `!x.IsTicketed` is checked inside the `UPDATE`, so a
ticketed order can never be requeued and **no duplicate OMS ticket can be raised by this path**.

### 8.2 The bulk resend is not atomic, despite the comment saying it is

`ATSRepository.EmailInvitations.cs:117-124`:

```csharp
		// One UPDATE per row rather than one for the set, because each invitation needs its
		// OWN freshly generated token - a shared token would let any candidate in the batch
		// open another candidate's form. They run inside the caller's transaction, so the
		// batch still commits or rolls back as a unit.
```

The second sentence is false for this caller. `ResendApplicationFormsAsync`
(`EndorsementSubmissionService.cs:538-643`) opens no transaction — `TransactionRunner` appears in
that file only at `:170` and `:269`, both inside other methods (`InsertEmailInvitationRequestAsync`
and a compensation path). `ExecuteUpdateAsync` bypasses the change tracker and executes
immediately, so each of the up-to-500 statements is its own implicit transaction.

Two ways this bites:

- A `CancellationToken` cancellation, or an `InternalServerException` from token generation at row
  *n* (§2.8), leaves rows `0..n-1` requeued — old links already dead — while the caller receives a
  500 and no count. There is no way to tell which rows moved.
- The `RecordManyAsync` at `:625` runs *after* the loop, so on that failure path **no history is
  written for the rows that did move**.

The ticketing side does not have this problem: one `ExecuteUpdateAsync`, genuinely atomic. If the
email side needs the same guarantee, the `foreach` belongs inside a
`TransactionRunner.RunAsync(_unitOfWork, …)` — or the loop should be restructured into one
`UPDATE` with a `CASE`/`VALUES` join so the per-row tokens survive.

### 8.3 History is written for rows that did not move (email side)

```csharp
		// Recorded for the rows the caller was allowed to act on. A row skipped because the
		// job is mid-send keeps its own history from that send, so no entry is lost.
		if (requeued > 0)
		{
			await _orderHistoryService.RecordManyAsync(
				inScopeIds,
```

The id set is `inScopeIds` — every id the caller was *allowed* to touch — not the rows the
`UPDATE` moved. The repository returns only an `int`, so the service **cannot** know which rows
moved. Select 5, 4 are mid-send, and the audit trail gains 5 `ApplicationFormResent` entries.

The inline comment defends the choice against *losing* an entry, and that defence holds. It does
not address the entries that are *wrong*: the order-history screen will show a resend that never
happened, attributed to a named user via `OrderHistoryFactory`'s `_currentUser.UserId`. This is
the mirror image of §8.4.

### 8.4 History is written from a pre-update read (ticketing side)

```csharp
		var eligibleIds = await _ticketingRepository.GetExhaustedTicketIdsAsync(
			inScopeIds,
			cancellationToken);

		var requeued = await _ticketingRepository.RequeueExhaustedTicketsAsync(
			eligibleIds,
			cancellationToken);
```

Two round trips, no transaction. A row the ticketing job claims in between stops matching the
`UPDATE` predicate but is still in `eligibleIds`, so it gets a `TicketRetryRequested` entry for a
retry that did not happen, and `RequeuedCount` returns lower than `eligibleIds.Count`.

Narrower than §8.3 — the read applies the same predicate the `UPDATE` does, so only a genuine race
can open the gap, and the reported count stays honest. But the comment above the `RecordManyAsync`
(*"Recorded only for orders that were eligible, so a stale id in the selection never produces a
history entry claiming a retry that did not happen"*) overstates it: it prevents entries for ids
that were **already** stale, not for ids that go stale between the two statements.

### 8.5 Neither bulk requeue is idempotent in the same way, and only one could be

`RequeueExhaustedTicketsAsync` is self-invalidating: after it runs, the row is `Pending` with
`TicketAttempts = 0`, so `TicketStatus == Error && TicketAttempts >= MaxTicketAttempts` no longer
matches and a second call returns 0. That is what
`RequeueExhaustedTicketAsync_ShouldBeIdempotent_WhenCalledTwice` pins.

`RequeueEmailInvitationsAsync` is **not**. After it runs the row is `Pending`, which is still
`!= Processing`, so a second call with the same ids matches again — issuing a second token,
resetting `EmailSendAttempts` to 0 again, and returning the same count. Two operators clicking
"Resend selected" on the same page both get `RequeuedCount == N` and `IsComplete == true`. The
`_isBulkResending` flag stops one browser doing it twice; nothing stops two browsers.

This is not a regression — `ResendApplicationForm_ShouldWorkMultipleTimesForSameRecord` asserts
exactly this behaviour for the single-row path and treats it as correct, because re-inviting a
candidate is a legitimate repeatable action. But it does mean the concurrency guarantee the design
doc generalises across both sides ("a row the job re-claimed a moment ago simply does not match")
holds **only on the ticketing side**. On the email side the guard is exclusively against racing
the job's in-memory claim, not against a second operator.

### 8.6 Scope: no gap found, but the rule is duplicated

Checked explicitly, since the endpoint takes caller-supplied ids. Both paths resolve
`IAtsAccessScopeResolver` before touching any id, throw `ForbiddenException` when it returns null,
and filter every id through a per-row predicate over an `AsNoTracking()` identity-only projection.
Out-of-scope ids are **dropped silently** — not counted separately, not reported, not thrown —
which matches the single-row paths' choice to answer 404 rather than 403. `NotFoundException`
fires only when *nothing* is in scope. Tests
`ResendApplicationForms_ShouldIgnoreInvitationsOutsideTheCallerScope` and
`ResendApplicationForms_ShouldThrowNotFound_WhenNothingIsInScope` pin both halves for the email
side; **the ticketing side has no equivalent test** (§7.2).

Compared with the single-row ticketing retry's three-way split
(`oms-auto-ticketing_code_explanation.md` §5): 403 for "no ATS access at all" survives identically
in both bulk paths; 404 for "unknown or out of scope" survives but only in the all-or-nothing
form; **409 for "no longer retryable" is deliberately absent**, replaced by the count shortfall.
That is the design doc's central decision and the code implements it faithfully.

The maintenance hazard is that `IsOwnerWithinScope` and `IsWithinScope` are two hand-written
copies of one rule in two services (§3), and each is *also* the rule the corresponding read paths
use. Changing one without the other silently widens or narrows what a bulk action can reach
relative to what the list shows.

---

## 9. Wiring — what is registered where

### 9.1 Backend DI — `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`

```csharp
		services.AddScoped<IATSRepository, ATSRepository>();
		services.Decorate<IATSRepository, ATSCacheRepository>();
```

`:47-48`, and the email-invitation facet forwards through the decorated aggregate at `:56`:

```csharp
		services.AddScoped<IEmailInvitationRepository>(provider => provider.GetRequiredService<IATSRepository>());
```

so the email requeue path runs **through** `ATSCacheRepository` and revokes two HybridCache tags
(§2.9). The ticketing repository is registered directly at `:71`, bypassing the decorator:

```csharp
	// Same reasoning: TicketStatus moves within one Quartz tick, and the claim
	// query must never be served from a cache.
	services.AddScoped<IOMSTicketingRepository, OMSTicketingRepository>();
```

so the ticketing requeue revokes nothing. **Do not "normalise" that registration to match its
siblings** — you would cache a queue.

The three services:

```csharp
		services.AddScoped<IOrderHistoryService, OrderHistoryService>();
```

`:97`, then

```csharp
		services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();
```

`:100`, and

```csharp
		services.AddScoped<IOMSTicketingMonitoringService, OMSTicketingMonitoringService>();
```

`:160`. Both requeue services are **scoped, not singleton** — which is what lets
`OrderHistoryFactory` read `ICurrentUser` and stamp the acting user (§2.11).

Carter discovers both `ICarterModule` implementations by assembly scan; neither is registered by
hand.

### 9.2 Frontend DI — `UI/FrontendWebassembly/ServiceConfig/FrontendServiceConfig.cs`

```csharp
		services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();
```

`:79`, `IBulkUploadService` at `:83`, `IOMSTicketingService` at `:85`. All three use the `"API"`
named `HttpClient` (gateway-facing, with `CookieHandler` + `InterceptorHandler`).

### 9.3 Three strings that must agree, with nothing enforcing it

For each of the two endpoints, the route exists as an independent literal in three assemblies:

| | Carter `MapPatch` | `ATSPaths.cs` `MatchPath` / `PathSet` | UI service URL |
|---|---|---|---|
| Email | `"resendapplicationforms"` | `"/ats/resendapplicationforms"` → `"/resendapplicationforms"` (`:614-622`) | `"ats/resendapplicationforms"` (`EndorsementSubmissionService.cs:258`) |
| Ticketing | `"retrytickets"` | `"/ats/retrytickets"` → `"/retrytickets"` (`:438-446`) | `"ats/retrytickets"` (`OMSTicketingService.cs:161`) |

The UI literal has **no leading slash**; the gateway `MatchPath` has one; the `PathSet` must equal
the Carter route with a leading slash. A typo in any of the three is a runtime 404, not a compile
error. Verify all four routes (both single and both bulk) at runtime with `GET /__routes` on the
gateway.

### 9.4 Two response shapes that must agree by property name

`ResendApplicationFormsEndpointResponse(int RequestedCount, int RequeuedCount, bool IsComplete)`
binds by name to `FrontendWebassembly.DTO.ATS.BulkRetryResultDTO`. `IsComplete` is computed on the
server and a settable field on the client — if the endpoint ever stopped serialising it, the client
would silently read `false` and every batch would report a shortfall. Same for
`RetryTicketsEndpointResponse`.

### 9.5 Hand-synced constants across the assembly boundary

`EndorsementSubmissionService.MaxBulkResendSize` and
`OMSTicketingMonitoringService.MaxBulkRetrySize` are both `500` and both `public const`, read by
their own validators. The UI enforces neither — it relies on `RowsPerPage="10"` and page-scoped
select-all to stay far below the cap (§5.4). Nothing warns an operator who builds a 501-id request
by paging; they get a 400 whose `detail` reaches the snackbar via `ReadErrorDetailAsync()`.

---

## 10. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| The `WHERE` in `RequeueEmailInvitationsAsync` (`ATSRepository.EmailInvitations.cs:130`) | `RequeueEmailInvitationAsync` (`:83`) | The two are documented as deliberately identical; the bulk one loops the single one's predicate. Diverging them means the row button and the bulk button disagree about eligibility (§4.2) |
| The `WHERE` in `RequeueExhaustedTicketsAsync` (`OMSTicketingRepository.cs:294`) | `RequeueExhaustedTicketAsync` (`:260`) **and** `GetExhaustedTicketIdsAsync` (`:226`) | Three places encode "exhausted". The third only decides which ids get history entries, so a mismatch there shows up as wrong audit rows, not wrong counts (§8.4) |
| `!x.IsTicketed` in either ticketing predicate | — | That clause is the only thing preventing a **duplicate OMS ticket**. Removing it is unrecoverable (§4.1) |
| `CanResend` (`BulkUploadSubjectsDialog.razor.cs:491`) | The `RequeueEmailInvitationsAsync` predicate | The browser rule is **stricter** than the server's. Tightening the server to match is the fix for §8.1; loosening `CanResend` without touching the server changes nothing except what the UI offers |
| `CanRetry` (`TicketingStatusComponent.razor.cs`) | `OMSTicketingRepository.MaxTicketAttempts` and `OrderTicketStatus.MaxAttempts` in `UI/FrontendWebassembly/DTO/ATS/OMSTicketingDTO.cs` | Hand-synced across an assembly boundary — see the OMS doc §6.2, §6.3 |
| `MaxBulkResendSize` or `MaxBulkRetrySize` | The matching validator in `ResendApplicationFormsHandler.cs:22` / `RetryTicketsHandler.cs:22` | Both read the constant, so the value stays in sync — but the *messages* are separate interpolated strings, and the service's message adds "Narrow the selection and try again." while the validator's does not |
| The `.Distinct()` in either service | The validator's `ids.Count` check | The validator counts raw ids, the service counts distinct. Reordering `.Distinct()` after the cap check would let a padded request through the validator (§2.7) |
| `IsOwnerWithinScope` (`EndorsementSubmissionService.cs:648`) | `IsWithinScope` (`OMSTicketingMonitoringService.cs:279`), and the read paths both mirror | Two hand-written copies of one rule in two services, written with different syntax. Neither is tested for the ticketing bulk path (§8.6) |
| The id set passed to `RecordManyAsync` | §8.3, §8.4 | Email passes `inScopeIds` (too many), ticketing passes `eligibleIds` (a pre-update read). Neither is the set the `UPDATE` touched |
| `OrderHistoryEventType.ApplicationFormResent` / `.TicketRetryRequested` | The `newStatus` argument at each call site | Email passes `OrderStatus.PendingCandidateInfo` because the requeue genuinely resets it; ticketing passes `string.Empty` because a ticket retry is not a lifecycle step. Swapping them corrupts the order-history timeline |
| A Carter route string | `PathSet` in `Path/ATSPaths.cs` **and** the URL literal in the UI service | Three independent literals in three assemblies (§9.3) |
| An endpoint response record's property name | `UI/FrontendWebassembly/DTO/ATS/BulkRetryResultDTO.cs` | JSON binds by name; `IsComplete` failing to bind reads as `false` and every batch reports a shortfall (§9.4) |
| `.ats-status-board-selection-*` in `ats.css` | `.ats-bulk-subjects-selection-*` in `BulkUploadSubjectsDialog.razor.css` | **Duplicated, not shared.** Both copies carry a "keep the two in step" comment and they are already out of step on the breakpoint (600px vs 720px) and on the accent token (§6) |
| The `@media (max-width: 600px)` block at `ats.css:1032` | `BulkUploadSubjectsDialog.razor.css:444` | One is 600px, the other 720px. Between those widths the two bars behave differently (**C3**) |
| `position: sticky` on either bar | Every ancestor's `overflow` | Sticky silently stops working if an ancestor sets it. The page relies on `.ats-console*` leaving it unset; the dialog relies on `.ats-scroll-dialog .ats-dialog-body` (`ats.css:1931`) being the scroll container |
| The bar's `background` | The `linear-gradient` + `var(--c-surface)` layering | The accent tint is translucent. Dropping the surface layer lets table rows show through while scrolling (§6.1) |
| `_pageSubjects` / `_pageOrders` assignment | The `RemoveWhere` prune 15 lines later | The prune reads `SelectableSubjects`/`SelectableOrders`, which derive from that field. Assigning after the prune, or forgetting it, lets a selection outlive its page (§5.3) |
| `RowsPerPage` on either table | The practical reach of select-all | Page-scoped select-all means the cap is a backstop against hand-built requests, not against the UI (§5.4) |
| The `ATSCacheRepository.RequeueEmailInvitationsAsync` wrapper | `CacheTags.Report`, `CacheTags.WithdrawnApplication` | Revocation is conditional on `requeued > 0`. The ticketing path revokes nothing at all because `IOMSTicketingRepository` bypasses the decorator (§9.1) |
| `ResendApplicationFormsAsync`'s transaction handling | §8.2 | There is none today, and the repository comment claims there is |
