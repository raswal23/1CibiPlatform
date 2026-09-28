# Employment verification decline reason — code explanation

Companion to [`employment-verification-decline-reason.md`](employment-verification-decline-reason.md).
That document explains *what* changed and *why*. This one is for someone about to edit the
code: the real call chain, the quoted lines that carry the correctness, the wiring that is
invisible from any single file, and the strings that have to agree across files with nothing
enforcing it at compile time.

---

## 1. The decline, end to end

### 1.1 The page opens a panel instead of submitting

`UI/FrontendWebassembly/Pages/EmploymentVerification/VerifyEmployment.razor` — the button
that used to call `RejectAsync` now calls `BeginDecline`, and the two branches swap:

```razor
@if (_isDeclining)
{
    <div class="ev-confirm-decline">
        <label class="ev-confirm-decline-label" for="decline-reason">
            What is inaccurate about these details?
        </label>
        ...
        <textarea id="decline-reason"
                  class="ev-confirm-decline-input"
                  maxlength="@DeclineReasonMaxLength"
                  @bind="_declineReason"
                  @bind:event="oninput"
                  disabled="@_isSubmitting"></textarea>
```

`@bind:event="oninput"` is what makes the counter live; the default `onchange` would only
update on blur. The confirm note ("By confirming, you acknowledge…") moved into the `else`
branch because it is about confirming and would be wrong beside a decline form.

### 1.2 `VerifyEmployment.razor.cs` — the guard and the call

```csharp
private async Task RejectAsync()
{
	var reason = _declineReason.Trim();

	// Client-side twin of the API's NotEmpty rule. Checked here so the contact gets
	// the message next to the box they left blank rather than a 400 from the server.
	if (reason.Length == 0)
	{
		_declineError = "Please tell us what is inaccurate about these details.";
		return;
	}

	_declineError = "";

	await CompleteAsync(
		reject: true,
		cancellationToken => VerificationService.RejectAsync(
			Token,
			reason,
			cancellationToken));
}
```

`CompleteAsync` is unchanged and still owns `_isSubmitting`, the failure enum and the
`_completed` flip, so a failed decline leaves the panel open with the text intact rather
than discarding what the contact typed. `CanSubmitDecline` (`!_isSubmitting &&
!string.IsNullOrWhiteSpace(_declineReason)`) drives the Submit button's `disabled`.

`DeclineReasonMaxLength = 1000` is declared here with a comment naming its API twin — see
§4.

### 1.3 UI service — one helper, two bodies

`UI/FrontendWebassembly/Services/EmploymentVerification/Implementation/EmploymentVerificationService.cs`.
`RejectAsync` gained the reason and passes a payload; `VerifyAsync` still passes none:

```csharp
public Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> RejectAsync(
	string token,
	string reason,
	CancellationToken cancellationToken = default) =>
	CompleteVerificationAsync(
		token,
		"reject",
		cancellationToken,
		new RejectEmploymentVerificationRequest(reason));
```

Inside the shared helper:

```csharp
var url = $"employmentverification/{action}/{Uri.EscapeDataString(token)}";

// Confirm posts nothing; decline has to carry the reason the endpoint now
// requires. One helper serves both so the problem-title mapping below is
// not duplicated.
var response = payload is null
	? await _httpClient.PostAsync(url, content: null, cancellationToken)
	: await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
```

This file keeps its hand-rolled `try/catch` + `ReadFailureAsync` rather than moving to
`ApiRequestExtensions.SendAsync<T>`, because `ReadFailureAsync` maps problem titles
(`TokenExpired` / `TokenAlreadyUsed` / `TokenNotFound`) onto `VerificationLinkFailure`, and
the page renders a different full-screen state per value. That mapping is the feature; the
generic helper has nowhere to put it.

### 1.4 Gateway — unchanged

`BackendAPI/Modules/EmploymentVerification/Path/EmploymentVerificationPaths.cs` already
declares the route, and only the *body* changed, so nothing here was edited:

```csharp
RouteId: "RejectEmploymentVerificationRequest",
MatchPath: "/employmentverification/reject/{token}",
...
	["PathPattern"] = "/api/employment-verification/reject/{token}"
```

`PathPattern`, not `PathSet`, because there is a `{token}` to substitute — `PathSet` would
forward the literal text `{token}`.

### 1.5 Endpoint — the body record

`Features/VerifyEmployment/Command/RejectRequest/RejectRequestEndpoint.cs`:

```csharp
public sealed record RejectRequestBody(string Reason);
```

```csharp
async (
	string token,
	RejectRequestBody body,
	ISender sender,
	CancellationToken cancellationToken) =>
{
	var result = await sender.Send(
		new RejectRequestCommand(token, body.Reason),
		cancellationToken);
```

The body is its own record rather than the command because the command also carries the
route token; binding the command directly would make Minimal APIs look for `token` in the
JSON as well. No `[FromBody]` attribute — a complex type binds from the body by default,
which is the convention `CreateRequestEndpoint` in this module already follows, and
`Microsoft.AspNetCore.Mvc` is not in the module's `GlobalUsing.cs`.

### 1.6 Validator and handler

`RejectRequestHandler.cs` holds the command, its validator and the handler, per the
vertical-slice rule that operation-specific records live in the operation folder:

```csharp
private const int ReasonMaxLength = 1000;

RuleFor(command => command.Reason)
	.NotEmpty()
	.WithMessage("Please tell us what is inaccurate about these details.")
	.MaximumLength(ReasonMaxLength)
	.WithMessage($"The reason must be {ReasonMaxLength} characters or fewer.");
```

`ValidationBehavior<,>` runs this before the handler, so an empty or oversized reason never
reaches the service. The handler forwards it:

```csharp
service.VerifyAsync(
	request.Token,
	reject: true,
	request.Reason,
	cancellationToken);
```

### 1.7 Service — where the reason is bound to the outcome

`Services/EmailVerification/EmploymentVerificationService.cs`, in `VerifyAsync`:

```csharp
// A reason belongs to the rejection it was typed against, so a confirmation
// stores null. Normalised to null rather than "" so the tracking view's
// em-dash-for-no-reason test has one shape to handle.
var responseNotes = reject && !string.IsNullOrWhiteSpace(reason)
	? reason.Trim()
	: null;
```

This is the single decision point. It is why a `Verified` row can never carry a reason even
if a caller passes one, and why the stored value is never an empty string.

### 1.8 Repository — the write and the single-use guard

`Data/Repository/EmploymentVerificationRepository.cs`:

```csharp
var affectedRows = await db.Requests
	.Where(request => request.Id == id)
	.Where(request =>
		request.Status == VerificationRequestStatus.Pending ||
		request.Status == VerificationRequestStatus.Sent)
	.ExecuteUpdateAsync(
		setters => setters
			.SetProperty(request => request.Status, status)
			.SetProperty(request => request.VerifiedAt, verifiedAt)
			.SetProperty(request => request.RejectedAt, rejectedAt)
			// Unconditional, like the two timestamps above: a reason belongs to
			// the rejection it was typed against, so any other terminal outcome
			// clears it rather than leaving a stale sentence beside a Verified row.
			.SetProperty(request => request.ResponseNotes, responseNotes),
		cancellationToken);
```

The `Pending || Sent` predicate is the single-use guard — two simultaneous clicks cannot both
record a response, and the loser returns `AlreadyCompleted`. Adding a fifth `SetProperty`
does not weaken it.

### 1.9 Cache decorator

`Data/Cache/EmploymentVerificationCacheRepository.cs` forwards the new argument and keeps
invalidating `RequestsTag` on success, so the tracking grid's cached `ListAsync` cannot
serve a pre-decline snapshot:

```csharp
var result = await repository.MarkRespondedAsync(
	id,
	status,
	respondedAt,
	responseNotes,
	cancellationToken);

if (result)
{
	await cache.RemoveByTagAsync(RequestsTag, cancellationToken);
}
```

---

## 2. The read back — Tracking

```text
Tracking.razor.cs LoadAsync
  -> UI EmploymentVerificationService.GetSentRequestsAsync
  -> GET employmentverification/getsentrequests
  -> GetSentRequestsEndpoint -> GetSentRequestsHandler
  -> EmploymentVerificationService.ListSentRequestsAsync
  -> repository.ListAsync                     (HybridCache, RequestsTag, 2 min)
  -> SentVerificationRequestDTO.FromEntity    (ResponseNotes mapped here)
  -> Tracking.razor cell
  -> EmploymentVerificationDisplay.GetInaccuracyReason
```

Only the two ends changed. `SentVerificationRequestDTO` gained `ResponseNotes` in the
positional record **and** in `FromEntity` — the record is positional, so a field added to
one and not the other is a compile error, which is the reason `FromEntity` is the only
construction site (verified: nothing else news one up).

The cell:

```razor
<MudTd Class="ev-reason-cell" DataLabel="Reason for inaccuracy">
    <span class="ev-reason-clamp" title="@request.ResponseNotes">
        @EmploymentVerificationDisplay.GetInaccuracyReason(request)
    </span>
</MudTd>
```

Two classes because a `-webkit-line-clamp` needs a box to apply to and a `<td>` is not one —
the same split `.ev-email-cell` (on the `MudTd`) and `.ev-stacked-cell` (on the inner span)
already use. Both live in `wwwroot/css/ev.css`, not in a scoped sheet, because `Tracking`
has no `.razor.css` and every other `ev-` table class is there. `DataLabel` is mandatory:
`Breakpoint.Sm` card mode at 960px uses it as the row label.

### 2.1 The relabel

`UI/FrontendWebassembly/SharedService/EmploymentVerificationDisplay.cs`:

```csharp
public const string InaccurateStatusLabel = "Verified with inaccuracy";

public static string GetDisplayStatus(SentVerificationRequestDTO request)
{
	if (request.Status == "Sent" && request.TokenExpiresAt < DateTime.UtcNow)
	{
		return "Expired";
	}

	return request.Status == "Rejected"
		? InaccurateStatusLabel
		: request.Status;
}
```

Three readers depend on that constant, and the second fails silently if it drifts:

| Reader | What happens if it uses a literal that drifts |
|---|---|
| `GetDisplayStatus` | the chip shows the raw enum name |
| `Tracking.razor.cs GetStatusCssClass` | **no error** — the unmatched case falls through to `ats-status-pill unknown` and the chip silently loses its colour |
| `Tracking.razor` stat tile label | the tile and the chip disagree |

`GetStatusCssClass` switches on the **displayed** value, so its case label had to change from
`"Rejected"` to the constant. `CountByStatus` switches on the **stored** value, so its
argument had to stay `"Rejected"`:

```razor
@* The label is the displayed status but the count argument is the stored one:
   CountByStatus reads request.Status, which stays "Rejected" in the database.
   Passing the display string here would silently count nothing. *@
<div class="ev-stat">
    <span>@EmploymentVerificationDisplay.InaccurateStatusLabel</span>
    <strong>@CountByStatus("Rejected")</strong>
</div>
```

`GetResponseRate` also reads stored values (`request.Status is "Verified" or "Rejected"`) and
needed no change.

---

## 3. The other two slices — diff only

**Confirm** (`Features/VerifyEmployment/Command/VerifyRequest/`): identical shape, no body
record, and the handler passes `reason: null` explicitly. Its validator is untouched — a
confirmation has nothing to explain.

**Lapsed send** (`EmploymentVerificationService.CreateAndSendAsync`): the third and only
non-human writer of a terminal response. When `SendEmailAsync` returns false it marks the row
`Expired` to release the segment, and now passes `responseNotes: null`:

```csharp
await _repository.MarkRespondedAsync(
	entity.Id,
	VerificationRequestStatus.Expired,
	DateTime.UtcNow,
	responseNotes: null,
	cancellationToken);
```

It stays `Expired` rather than `Rejected` for the reason already commented in that method:
`Rejected` means the employer answered no and blocks the segment for good, so reusing it here
would hide a delivery failure as a decline and strand the segment permanently.

---

## 4. Wiring, and strings that must agree by hand

**No DI change.** The service, repository and Scrutor decorator were already registered in
`ServiceConfig/EmploymentVerificationServiceConfiguration.cs`; nothing new was introduced.
Note for test fixtures that `IEmailService` is keyed — `[FromKeyedServices("ats")]` — so a
mock is passed positionally in unit tests rather than resolved.

**No migration, no model-snapshot edit.** `ResponseNotes` has existed since
`20260814052157_InitialEmploymentVerification`. If you are tempted to add one, check the
snapshot first.

Nothing enforces these pairs at compile time:

| Pair | Files | Failure if they drift |
|---|---|---|
| `1000` | `RejectRequestCommandValidator.ReasonMaxLength` ↔ `VerifyEmployment.razor.cs DeclineReasonMaxLength` | the counter and `maxlength` let a contact type past the cap; the API then 400s on submit and the link is still single use, so their answer is lost |
| `"Rejected"` | `GetDisplayStatus`, `GetResponseRate`, `CountByStatus` argument | a tile counting nothing, or a rate that stops counting declines |
| `InaccurateStatusLabel` | `EmploymentVerificationDisplay`, `GetStatusCssClass`, tile label | chip silently goes grey (§2.1) |
| `ResponseNotes` | backend positional record ↔ UI `SentVerificationRequestDTO` property | JSON name mismatch deserialises to null; the column renders an em dash on every row and looks like no one ever gave a reason |
| `ev-reason-cell` / `ev-reason-clamp` | `Tracking.razor` ↔ `wwwroot/css/ev.css` | unstyled free text that widens the table |
| email hex ↔ `--c-ev-*` | `EmploymentVerificationService.cs` body literal ↔ `wwwroot/css/theme.css` | the email keeps the old theme; nothing fails, it just looks wrong |

---

## 5. Tests

`Test/Test/BackendAPI/Modules/EmploymentVerification.UnitTests/`:

- `RejectRequestValidatorTests.cs` — empty/whitespace/newline reason rejected, over-cap
  rejected, a good reason accepted, and the token rules still enforced alongside the new one.
- `DeclineReasonPersistenceTests.cs` — all three terminal writers. A decline stores the
  *trimmed* reason; a confirmation stores null **even when a reason is passed**; a failed send
  stores null. The third test uses a `MockBehavior.Strict` repository configured with a
  literal `null` in the notes position, so any reason reaching it leaves the call unmatched
  and fails.

`BlockedSegmentPredicateTests` is untouched and must stay green — it pins that `Rejected`
still blocks its segment, which is the invariant the display-only relabel depends on.

---

## 6. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| `InaccurateStatusLabel` | `GetStatusCssClass`, the stat tile | three readers, one of which fails silently to grey |
| `GetDisplayStatus` | `Tracking.FilteredRequests`, the search placeholder | search matches the displayed value, so the words an operator can search change with it |
| `MarkRespondedAsync` signature | all three call sites + the cache decorator | two are non-obvious: the confirm handler's `reason: null` and the failed-send path |
| The `Pending \|\| Sent` predicate | `DeclineReasonPersistenceTests`, the single-use behaviour | it is the concurrency guard, not a filter |
| `ReasonMaxLength` | `DeclineReasonMaxLength` in the page | separate projects, no shared constant |
| `ResponseNotes` name or nullability | backend `FromEntity`, the UI DTO, `GetInaccuracyReason` | JSON-name coupling across the wire |
| The email palette | the comment block above the body literal, `theme.css` | hand-synced; a token change does not propagate |
| `VerifyEmployment.razor` panel markup | `VerifyEmployment.razor.css`, the 560px block | `.ev-confirm-decline` overrides the nested `.ev-confirm-actions` padding by specificity, and needs its own mobile padding |
| `Tracking` columns | `ColumnCount` | it drives the skeleton loading rows; a mismatch shows a broken placeholder |
| The reject route | `EmploymentVerificationPaths`, `RejectRequestEndpoint`, the UI service's `action` string | three files agree on `/reject/{token}` with nothing enforcing it |
