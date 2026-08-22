# ATS + OnePlatform review — what was fixed and how

Companion to [ats-oneplatform-code-review.md](ats-oneplatform-code-review.md). One
section per numbered finding: what was wrong, the code that changed, and why it was done
that way.

> For the full before/after code on each item — including the alternatives considered and
> rejected — see [ats-oneplatform-fix-details.md](ats-oneplatform-fix-details.md).

Implemented on 2026-08-22 against `dev` @ `63758ba8`.

**Result:** all 15 findings addressed. `dotnet build 1CibiPlatform.sln` — 0 errors.
`dotnet test` — **547 passed, 0 failed** (was 528; 19 new tests).

50 files changed, 5 added.

---

## Summary

| # | Finding | Fix | Tests |
|---|---|---|---|
| 1 | Anonymous form POST trusted a body-supplied ID | Token resolved server-side; body ID overwritten | +6 |
| 2 | SignalR hub joined any group named in the query | Group from `Context.User`; `[Authorize]` | — |
| 3 | Downloads accepted caller-supplied file keys | Contract takes doc *types*; keys resolved under scope | +3 |
| 4 | `GetReportResult` had no scope check | Scope pushed into the query; 404 on miss | +1 |
| 5 | NRE on unknown report id | Null check moved before the dereference | covered |
| 6 | Per-request state in service fields | Local `UploadedKeys` collector | covered |
| 7 | `result.Result` read as sync-over-async | Renamed to `queryResult` | — |
| 8 | `async void` handlers | `async Task` / observed continuation | — |
| 9 | `NewOrderComponent` never unsubscribed | `IDisposable`; `IAsyncDisposable` on the service | — |
| 10 | CSV preview mis-parsed quoted fields | RFC 4180 parser + 25 MB limit + row cap | +10 |
| 11 | Dashboard loaded every invitation | Date-bounded query; `RecentOrders` capped at 25 | +2 |
| 12 | Swagger exposed in Production | Removed from the Production branch | — |
| 13 | Assistant stores grew unbounded | Idle eviction after 2 h, locks disposed | — |
| 14 | Role ladder duplicated in 5 services | All 5 delegate to `AtsAccessScopeResolver` | covered |
| 15 | No UI test coverage | `Test/Test/UI/` with the CSV parser suite | +10 |

---

## 1. Anonymous application-form POST accepted a caller-supplied `EmailInvitationID`

**Severity: Critical.** Unauthenticated write access to PII.

### What was actually wrong

The hash-token design was already in place and correct in three other places —
`IsHashTokenValidAsync` checked expiry properly, PhilSys used it via `IATSQueries`, the
form *load* was gated by it, and `WithdrawnApplicationForm` was token-authorized end to
end. The client also gated rendering on `IsExpired` / `Done` / `Withdrawn`
(`ATSApplicationForm.razor.cs:35`).

The gap was narrow and specific: **the token gated the read, not the write.** At HEAD,
`AddApplicationFormDataRequest` had no `HashToken` field, so the token never reached the
server on POST. Identity came from `PersonalDetailsDTO.EmailInvitationID` — a plain Guid
in the multipart body. A caller who skipped the UI never ran the client-side gate, and
the endpoint had nothing of its own to bypass.

Guid v7 is not a mitigation: it is timestamp-derived, and `getemailidandapplicationformpath`
hands it to the browser anyway.

### The fix

Reused the existing pattern rather than inventing one. `AuthorizeApplicationFormAsync`
does what `WithdrawnApplicationForm` already did — resolve from the token — plus the two
checks the load path lacked.

New DTO, `BackendAPI/Modules/ATS/DTO/ApplicationFormClaimDTO.cs`:

```csharp
public record ApplicationFormClaimDTO
{
    public Guid EmailInvitationID { get; init; }
    public DateTime? HashTokenExpiration { get; init; }
    public string? ApplicationFormStatus { get; init; }

    public bool IsExpired => !HashTokenExpiration.HasValue
        || HashTokenExpiration.Value <= DateTime.UtcNow;
}
```

`ApplicationFormService.AuthorizeApplicationFormAsync`:

```csharp
private async Task<Guid> AuthorizeApplicationFormAsync(string hashToken, CancellationToken ct)
{
    var claim = await _atsRepository.GetApplicationFormClaimAsync(hashToken, ct);

    if (claim is null || claim.EmailInvitationID == Guid.Empty)
        throw new NotFoundException("No record found for the provided hash token.");

    if (claim.IsExpired)
        throw new BadRequestException("This application form link has expired. Please request a new one.");

    // Withdrawn and Done are both terminal. Without this the second post would fail
    // on the PersonalDetails 1:1 unique constraint as an opaque 500.
    if (!string.Equals(claim.ApplicationFormStatus, ApplicationFormStatus.Pending, StringComparison.OrdinalIgnoreCase))
        throw new ConflictException($"This application form has already been {claim.ApplicationFormStatus?.ToLowerInvariant() ?? "processed"}.");

    return claim.EmailInvitationID;
}
```

The body value is **overwritten, not compared** — comparing would still leak which ids
exist:

```csharp
var emailInvitationId = await AuthorizeApplicationFormAsync(hashToken, ct);

personalDetails.EmailInvitationID = emailInvitationId;
addressDetails.EmailInvitationID = emailInvitationId;
educationalBackground.EmailInvitationID = emailInvitationId;
licensesDetails.EmailInvitationID = emailInvitationId;
professionalExperiences.EmailInvitationID = emailInvitationId;
referenceDetails.EmailInvitationID = emailInvitationId;
signatureDetails.EmailInvitationID = emailInvitationId;
```

The claim lookup is **deliberately not cached and not decorated** — a stale expiry would
keep a spent link working:

```csharp
// ATSCacheRepository — pass-through by design.
public async Task<ApplicationFormClaimDTO?> GetApplicationFormClaimAsync(string hashToken, CancellationToken cancellationToken)
{
    return await _atsRepository.GetApplicationFormClaimAsync(hashToken, cancellationToken);
}
```

Also fixed: `GetEmailIdAndApplicationFormPathAsync` returned a usable `EmailId` for
**expired** tokens, because its query filters on `HashToken` alone while the expiry check
lived in the never-called `IsHashTokenValidAsync`. It now 400s server-side instead of
relying on the client to honour `IsExpired`.

Rejected exceptions are re-thrown rather than flattened to 500:

```csharp
if (ex is NotFoundException or BadRequestException or ConflictException)
    throw;
```

Added `ConflictException` (`BuildingBlocks/Exceptions/`) plus its 409 arm in
`CustomExceptionHandler` — a spent form is "not any more", not "you may not".

**This is defence in depth, not a replacement.** The client gate stays: it gives the
candidate a proper "link expired" screen instead of a raw 400.

**Tests:** 6 new — unknown token, empty token, expired token, already-submitted (409),
withdrawn (409), and the key one:

```csharp
[Fact]
public async Task AddApplicationFormData_WithMismatchedEmailInvitationId_ShouldBindToTokenOwner()
{
    // A caller posts a valid token of their own but substitutes somebody else's
    // EmailInvitationID in the body. The body value must be ignored entirely.
    await SeedEmailInvitationRequestData();
    var victimEmailId = Guid.CreateVersion7();
    var command = BuildValidCommand(SeededHashToken, claimedEmailId: victimEmailId);

    var result = await _sender.Send(command);

    result.IsAdded.Should().BeTrue();
    _dbContext.PersonalDetails.Any(p => p.EmailInvitationID == EmailId).Should().BeTrue();
    _dbContext.PersonalDetails.Any(p => p.EmailInvitationID == victimEmailId).Should().BeFalse();
}
```

### Rate limiting

Done through **your existing gateway mechanism** (`RouteDefinitionDTO.Metadata`), not a
second limiter on the API host. New policy in `GatewayConstants.RateLimitPolicies`:

```csharp
public const string AnonymousApplicationForm = "AnonymousApplicationForm";
```

Partitioned by client IP rather than by policy name — a shared bucket would let one
caller starve every candidate filling in a form:

```csharp
GatewayConstants.RateLimitPolicies.AnonymousApplicationForm => RateLimitPartition.GetFixedWindowLimiter(
    httpContext.Connection.RemoteIpAddress?.ToString() ?? policyName,
    _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 30,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0
    }),
```

Applied to all three anonymous ATS routes in `ATSPaths.cs` via `Metadata`, exactly as
`AuthPaths.cs` does for `LoginPolicy`.

---

## 2. SignalR hub joined any group named in the query string

**Severity: High.** No credentials required.

`MapHub<ATSHub>` had no authorization and the group came from
`Request.Query["userId"]`. Connecting to `/hubs/atsbulk?userId=<victim-guid>` joined the
victim's group and received their bulk-upload notifications and AI-assistant responses —
which stream candidate names and order statuses. `AIAgentHub` had the identical flaw and
also dropped its `AddToGroupAsync` task unawaited.

### Why not inject `ICurrentUser`

You noted `ICurrentUser` already extracts JWT claims. It does — but it is scoped and
backed by `IHttpContextAccessor`, and `HttpContext` is null for hub invocations after the
handshake. So the hub reads `Context.User` (the same validated principal) through a
shared helper that mirrors `CurrentUser`'s claim order:

`BuildingBlocks/SignalR/HubCallerContextExtensions.cs`:

```csharp
public static string? GetUserGroupName(this HubCallerContext context)
{
    var value = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrWhiteSpace(value))
        value = context.User?.FindFirst(UserIdClaim)?.Value;

    return Guid.TryParse(value, out var userId) && userId != Guid.Empty
        ? userId.ToString()
        : null;
}
```

Both hubs now:

```csharp
[Authorize]
public class ATSHub : Hub<IATSClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetUserGroupName();

        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        await base.OnConnectedAsync();
    }

    // SignalR removes a connection from its groups when it disconnects.
}
```

No `AccessTokenProvider` was needed: the JWT already arrives via the HttpOnly auth
cookie, which the browser sends on the WebSocket handshake. Client-side, `?userId=` was
dropped from both `EndorsementSubmissionService` and `AIChatService` — the latter had a
`Guid.CreateVersion7()` fallback when local storage was empty, so the connection was not
authenticated in any sense.

`OnDisconnectedAsync` was deleted from both: SignalR removes connections from groups
automatically.

---

## 3. Report downloads accepted caller-supplied object-storage keys

**Severity: High.**

The validator checked `FileKey` was non-empty and under 255 chars, then `ReportService`
passed it straight to `_objectStorageService.DownloadAsync`. Any authenticated user —
including one scoped to a single client — could zip up any object in the bucket.

### The fix: change the contract, don't validate the keys

The UI selects document *types*, so the wire format never needed keys at all:

```csharp
public class DownloadIndividualDocumentsRequestDTO
{
    public Guid EmailInvitationRequestId { get; set; }
    public List<string> DocumentTypes { get; set; } = [];
}

public static class AtsDocumentTypes
{
    public const string BiometricPhoto = "BiometricPhoto";
    public const string Resume = "Resume";
    // ... GovernmentId, Diploma, Coe, ConsentForm, Report
}
```

The service resolves the order under scope, then maps types to keys:

```csharp
if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
    throw new NotFoundException($"No documents found for email invitation ID {...}.");

var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(
    downloadInvididualRequest.EmailInvitationRequestId,
    scope.AuthorizedClientIds,
    scope.RequiredOwnerId,
    cancellationToken);

if (result is null)
    throw new NotFoundException(...);

var files = ResolveRequestedDocuments(result, requested).ToList();
```

The zip filename now comes from the order the caller was allowed to read, not from a
`SubjectName` they supplied — so the service returns `(Stream, string SubjectName)`.

`DownloadMultipleOrderRecordsAsync` had the same shape and got the same treatment;
`GetDownloadDocumentsAsync` gained scope parameters applied **inside** the query:

```csharp
.Where(eir => (authorizedClientIds == null
        || (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
    && (!requiredRequestorId.HasValue
        || eir.RequestorId == requiredRequestorId.Value))
```

**Tests:** 3 new, including one that proves the cross-client case:

```csharp
[Fact]
public async Task DownloadIndividualReportAsync_ShouldThrowNotFound_WhenOrderBelongsToAnotherClient()
```

---

## 4. `GetReportResult` had no access-scope check

**Severity: High.** Also the endpoint that *supplied* the keys for finding 3.

`GetReportsAsync` built a full scope; `GetReportResultByEmailInvitationRequestIdAsync`
took an id and queried it. The scope is now pushed into the `Where`, and the miss returns
`NotFoundException` rather than `Forbidden` so a caller cannot probe which ids exist.

The cache key had to change too, or the first caller would populate an entry every other
caller then shares:

```csharp
var cacheKey = $"report_result_{emailInvitationRequestId}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";
```

---

## 5. NRE on unknown report id

`ATSRepository.cs:764` dereferenced `result!.Educational` before the service's null check
could run, turning an intended 404 into an unhandled 500. `result!.HitStatus` on line 297
had the same bug one frame up. The check moved ahead of both:

```csharp
// An unknown id - or one outside the caller's scope - returns null here. The
// null-forgiving dereference below used to turn that into a 500.
if (result is null)
    return null;
```

---

## 6. Per-request state in service instance fields

`ApplicationFormService` held 15 `private string ...Key = ""` fields collecting uploaded
object keys for the rollback path. Safe today (scoped registration), but a change to
singleton — or reuse from a background scope — would make form B's cleanup delete form
A's files.

Replaced with a local collector threaded through the private methods:

```csharp
private sealed class UploadedKeys
{
    private readonly List<string> _keys = [];

    /// <summary>Records a key as owned by this submission and returns it.</summary>
    public string Track(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _keys.Add(key);

        return key;
    }

    public IReadOnlyList<string> All => _keys;
}
```

Call sites read `resumeFileKey = uploadedKeys.Track(await _objectStorageService.UploadAsync(...))`,
which also removes the ordering coupling where `AddEducationalBackgroundDataAsync` read
fields set by an earlier call. Rollback becomes `foreach (var key in uploadedKeys.All)`.

Also deleted the leftover debug local at line 355:
`var test = BitConverter.ToString(signatureBytes.Take(16).ToArray());`

---

## 7. `result.Result` read as sync-over-async

Investigated and **it was not a bug** — `result` is the awaited record and `.Result` is
its DTO property, not a `Task`. But it reads exactly like a thread-pool block, which is
why it was flagged. Renamed for clarity:

```csharp
// Named queryResult rather than result: `result.Result` reads like a
// sync-over-async block on a Task, and it is not one.
var queryResult = await sender.Send(query, cancellationToken);
return Results.Ok(new GetBulkUploadSubjectsEndpointResponse(queryResult.Result));
```

---

## 8. `async void` event handlers

`Otp.HandleResendOtp` is called from `OnClick`, which accepts `Task` — a clean change to
`async Task`.

`NewOrderComponent.OnATSResponse` is bound to a plain `Action` event, so it must stay
void at the delegate boundary. The body moved into an `async Task` that is observed:

```csharp
private void OnATSResponse(string message)
{
    _ = OnATSResponseAsync(message);
}

private async Task OnATSResponseAsync(string message)
{
    try
    {
        await InvokeAsync(() => { Snackbar.Add(message, Severity.Success); StateHasChanged(); });
    }
    catch (ObjectDisposedException)
    {
        // The component went away between the notification arriving and the render.
    }
}
```

The two empty `catch { }` blocks in `EndorsementSubmissionService.StartAsync` — which
swallowed these throws a second time — now log.

---

## 9. `NewOrderComponent` never unsubscribed

`EndorsementSubmissionService` lives for the app lifetime, so every visit to New Order
added another subscription to a live delegate holding a disposed component. The
user-visible symptom: one bulk-upload notification appearing 3, 4, 5 times, then none at
all once the disposed components started throwing into the `catch { }`.

```razor
@inherits CrudPageBase
@implements IDisposable
```

```csharp
public void Dispose()
{
    EndorsementSubmissionService.ATSResponseReceived -= OnATSResponse;
}
```

`IEndorsementSubmissionService` also gained `IAsyncDisposable` to dispose the hub
connection. `AIAssistantComponent` and `AIChat` already did this correctly — this brings
`NewOrderComponent` in line with them.

---

## 10. CSV preview mis-parsed quoted fields

The preview split on `,` and `\n` by hand while the backend used CsvHelper, so
`"Dela Cruz, Jr.",Juan,...` previewed as six misaligned columns — the operator was
approving a preview that did not match the import.

CsvHelper is only referenced by the ATS backend; adding it to the WASM project would grow
the download payload, so `SharedService/CsvPreviewParser.cs` implements a single-pass
RFC 4180 scan instead. Quotes toggle field mode, `""` is a literal quote, newlines inside
quotes belong to the field.

Two more bugs fixed in the same method:

- `OpenReadStream()` had **no** `maxAllowedSize`, so it used Blazor's 512 KB default and
  threw on any larger CSV — while the upload path right below it allowed 25 MB. A 600 KB
  file failed at preview but was perfectly uploadable. Now both are 25 MB.
- A 10k-row CSV built a `List<List<string>>` of every cell in the browser before the
  dialog opened. Capped at 100 rows — and the cap is **stated**, not silent:

```csharp
var previewMessage = previewData.IsTruncated
    ? $"Showing the first {previewData.Rows.Count} of {previewData.TotalRowCount} rows. All rows will be uploaded."
    : "Upload has been disabled. Blank detail is not allowed.";
```

---

## 11. Dashboard loaded every invitation row

`GetDashboardDataAsync` did `.Include(i => i.ReportDetails).ToListAsync()` with **no date
bound**. For a super admin that materialised the entire `EmailInvitationRequest` table
plus every related report row on every dashboard load.

`CreateDashboard` discards everything outside the current year anyway, so the query is
now bounded:

```csharp
var windowStart = yearStart < now.AddMonths(-DashboardWindowMonths)
    ? yearStart
    : now.AddMonths(-DashboardWindowMonths);
```

Rows with no `OrderCreatedAt` are kept — the candidate-response tiles count them by email
status, which does not depend on the order date.

Separately, `RecentOrders` projected **every** invitation in scope and shipped it to a UI
that never renders it (no consumer exists outside the DTO). Capped at 25.

**Tests:** 2 new — one asserting the window reaches back at least to the start of the
calendar year, one asserting the 25-row cap with newest-first ordering.

---

## 12. Swagger exposed in Production

```csharp
if (app.Environment.IsProduction())
{
    // Swagger stays off here. It publishes a full map of every endpoint, DTO and
    // parameter - including the anonymous application-form routes - to anyone who
    // can reach the host. Sandbox and UAT already omit it; Production had
    // inherited the Development branch by copy.
    await DatabaseExtensions.IntializeDatabaseAsync(app);
}
```

**Still worth a decision:** `IntializeDatabaseAsync` runs automatically at Production
startup, so a migration applies on deploy with no human in the loop. Left as-is — that is
an operational call, not a code fix.

---

## 13. Assistant stores grew without bound

`AtsChatHistoryStore` capped each user's history at 20 messages but never removed a user's
entry, and `GetUserLock` added a `SemaphoreSlim` per user disposed only by an explicit
`Clear`. In a process-lifetime singleton that grows with users who have *ever* chatted,
not users currently chatting.

Entries now carry `LastAccessedUtc` and are evicted after 2 hours idle, disposing the lock
with them. Sweeping happens opportunistically on access rather than on a timer — no
background machinery, and the pass is bounded by recently-active users.

---

## 14. Role ladder duplicated in five services

`AtsAccessScopeResolver` existed and its own doc comment listed the five services still
carrying inline copies. Findings 3 and 4 were both services that had to *remember* to
apply the ladder and didn't — five copies is five chances to forget.

All five now delegate:

| Service | Before | After |
|---|---|---|
| `ReportService` | ~40 lines inline | `_accessScopeResolver.ResolveAsync` |
| `DashboardService` | ~40 lines inline | same |
| `DisputeOrderService` | ~40 lines inline | same |
| `EndorsementSubmissionService` | ~40 lines inline | same |
| `AtsAssistantPlugin` | ~40 lines inline | same |

The shape is uniform:

```csharp
// The role ladder lives in AtsAccessScopeResolver now - this used to be an
// inline copy of it.
if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
    return CreateEmptyResult(paginationRequest);

var clientIds = scope.AuthorizedClientIds;
var requiredRequestorId = scope.RequiredOwnerId;
```

`ReportService` and `DashboardService` dropped `ICurrentUser` and `IUserClientRepository`
entirely. `AtsAssistantPlugin` kept `ICurrentUser` (it still reads `UserId`/`AtsClientId`
for draft ownership) but lost `IUserClientRepository`.

**On the tests:** where a test's *subject* is the ladder itself
(`WithdrawnApplicationFilteringTests`, `AtsAssistantPluginTests`,
`DisputeOrderServiceIntegrationTests`), they construct a **real** `AtsAccessScopeResolver`
over the existing mocks rather than stubbing it — otherwise the fix would have deleted
the coverage. Note `Mock.Of<IAtsAccessScopeResolver>()` returns null, which the services
correctly read as "no access"; that caused 6 initial failures, since fixed.

---

## 15. No UI test coverage

`Test/Test/UI/` now exists (the `.csproj` already referenced the UI project and declared
an empty `UI\` folder). Started with the highest-value target: `CsvPreviewParser` is plain
C# needing no bUnit, and it is where a silent regression is most expensive.

10 tests covering headers/rows, commas inside quotes, doubled-quote escapes, newlines
inside quotes, CRLF, blank-row handling, empty input, headers-only, the row cap with
`IsTruncated`, and unquoted-whitespace trimming.

The remaining candidates from the review — the `ApplicationFormComponent` draft
round-trip and the `CanAddEmployer2/3` boolean chains — need bUnit and are left as
follow-up.

---

## Verification

```
dotnet build 1CibiPlatform.sln    →  0 errors
dotnet test Test/Test/Test.csproj →  547 passed, 0 failed, 0 skipped (31s)
```

Integration tests ran against real Testcontainers PostgreSQL.

Test count went 528 → 547. No test was deleted to make the suite pass; the 6 that failed
mid-refactor were tests whose subject had moved, and each was repointed at the new
seam rather than removed.

## Follow-ups not done

- **Production auto-migration** (finding 12) — `IntializeDatabaseAsync` at Production
  startup is an operational decision, not a code fix.
- **bUnit component tests** (finding 15) — the draft round-trip and employer-validation
  chains.
- **Dashboard SQL aggregation** (finding 11) — the query is now bounded, which removes the
  urgency; pushing `GROUP BY date_trunc` and `COUNT(*) FILTER` into SQL remains the
  eventual fix if volume grows.
