# ATS + OnePlatform code review — findings and remediation plan

Reviewed on 2026-08-22 against `dev` @ `63758ba8`.

> **Status: all 15 findings fixed** (2026-08-22). Build clean, 547 tests passing.
> - [ats-oneplatform-code-review-fixes.md](ats-oneplatform-code-review-fixes.md) — summary of what changed
> - [ats-oneplatform-fix-details.md](ats-oneplatform-fix-details.md) — per-item before/after code and rationale

Scope: `BackendAPI/Modules/ATS` (289 files), `UI/FrontendWebassembly` (345 files), plus
the shared surfaces they lean on (`Auth.Shared`, `BuildingBlocks`, YARP gateway, API host
pipeline).

**Baseline:** `dotnet build 1CibiPlatform.sln` succeeds — 0 errors, 139 warnings (all
nullability warnings, almost entirely in `Test/`).

---

## Executive summary

The architecture is in good shape. The recent refactors landed well: the keyset pagination
collapse (`08d3af33`) genuinely simplified the surface, the DB-claim job pattern
(`aa8f1731`) with `FOR UPDATE SKIP LOCKED` is a correct and well-commented replacement for
the Redis dependency, `AtsAccessScopeResolver` is the right abstraction, and the ATS
integration test suite is real (≈150 tests over the module).

The problems are concentrated in **authorization** rather than structure. Four issues let a
caller reach data or actions they should not, and one is remotely exploitable without any
credentials. Everything else is correctness/robustness work of decreasing urgency.

| # | Finding | Severity | Effort |
|---|---|---|---|
| 1 | Anonymous application-form POST accepts a caller-supplied `EmailInvitationID` | **Critical** | M |
| 2 | SignalR hub joins any group named in a query string | **High** | S |
| 3 | Report downloads accept caller-supplied object-storage file keys | **High** | M |
| 4 | `GetReportResult` has no access-scope check (IDOR) | **High** | S |
| 5 | `GetReportResultByEmailInvitationRequestIdAsync` NREs on unknown id | Medium | XS |
| 6 | `ApplicationFormService` holds per-request state in instance fields | Medium | S |
| 7 | `GetBulkUploadSubjectsEndpoint` blocks on `.Result` | Medium | XS |
| 8 | `async void` event handlers swallow exceptions | Medium | XS |
| 9 | `NewOrderComponent` never unsubscribes from the hub event | Medium | XS |
| 10 | Bulk CSV preview parser does not handle quoted fields | Medium | S |
| 11 | Dashboard loads every invitation row into memory | Medium | M |
| 12 | Swagger UI is exposed in Production | Medium | XS |
| 13 | `AtsChatHistoryStore` / `AtsOrderDraftStore` grow without bound | Low | S |
| 14 | Duplicated role-ladder logic in 5 services | Low | M |
| 15 | Zero UI test coverage | Low | L |

---

## Critical

### 1. Anonymous application-form POST accepts a caller-supplied `EmailInvitationID`

`BackendAPI/Modules/ATS/Features/AddApplicationFormData/AddApplicationFormDataEndpoint.cs:16`

The endpoint has no `.RequireAuthorization()` — correctly so, since candidates fill this
form from an emailed link without an account. But it also never validates the hash token
that is supposed to authorize that link. The candidate identity comes from
`PersonalDetailsDTO.EmailInvitationID`, a plain `Guid` field on the multipart form body
that the browser sets (`ApplicationFormComponent.razor.cs:797`).

Compare the sibling anonymous endpoints in `EmploymentVerification`, which all take a token
and call `.AllowAnonymous()` deliberately. Here the token check is simply absent:
`hashToken` appears nowhere in the `AddApplicationFormData` feature folder or in
`ApplicationFormService.AddApplicationFormDataAsync`.

**Failure scenario.** An unauthenticated caller enumerates or guesses an
`EmailInvitationID` and POSTs a complete application form for it. The handler writes
personal details, address, education, employment history, references and a signed consent
PDF against another person's invitation, then flips `ApplicationFormStatus` to `Done` and
`OrderStatus` to `InProgress`. The real candidate's link now resolves to a completed form.
Because `PersonalDetails.EmailInvitationID` is a 1:1 FK
(`PersonalDetailsConfiguration.cs`), a legitimate later submission throws on the unique
constraint. This is unauthenticated write access to PII on a background-check platform.

Guid v7 ids are *not* a mitigation — they embed a timestamp and are handed to the browser
by `getemailidandapplicationformpath`, so they are neither secret nor unpredictable.

**Fix.** Carry the hash token through the submission, exactly as `WithdrawnApplicationForm`
already does:

1. Add `HashToken` to `AddApplicationFormDataRequest` / `...Command`, and post it from
   `ApplicationFormComponent` (the component already holds it — it used the token to fetch
   `EmailId` on load).
2. In `ApplicationFormService.AddApplicationFormDataAsync`, resolve the invitation *from
   the token* via `GetEmailIdAndApplicationFormPathAsync` and use the resolved
   `EmailInvitationID` for every child entity. Ignore the client-supplied value entirely —
   do not merely compare them.
3. Reject when the token is unknown, expired (`HashTokenExpiration <= UtcNow`), or the
   invitation is not in `ApplicationFormStatus.Pending` — that last check also makes
   double-submission a clean 409 instead of a DB constraint violation.
4. Apply the same treatment to `getemailidandapplicationformpath`
   (`GetEmailIdAndApplicationFormPathEndpoint.cs:11`). It is anonymous and correctly keyed
   by token, but note it returns data for *expired* tokens: the repository query filters on
   `HashToken` only, while `IsHashTokenValidAsync` checks expiry and is not called here.
5. Rate-limit both anonymous ATS endpoints by IP.

Add integration tests for: wrong token, expired token, already-submitted invitation, and
token/body-id mismatch.

---

## High

### 2. SignalR hub joins any group named in a query string

`BackendAPI/Modules/ATS/Hubs/ATSHub.cs` (`OnConnectedAsync`)

`MapHub<ATSHub>` in `AppConfiguration.cs:75` has no `.RequireAuthorization()`, and the hub
takes the group name straight from `Request.Query["userId"]` with no comparison against
`Context.User`.

**Failure scenario.** Anyone connects to `/hubs/atsbulk?userId=<victim-guid>` and joins the
victim's group. They then receive every `ReceiveATSResponse` bulk-upload notification (which
includes uploaded file names) and every `ReceiveChatResponse` from the AI assistant — the
assistant streams order lookups containing candidate names and statuses to that same group
(`AtsAssistantService.AskAsync`). Client-side, `EndorsementSubmissionService.StartAsync`
even falls back to `Guid.CreateVersion7()` when local storage has no `UserId`, so the
connection is not authenticated in any sense.

**Fix.** Add `.RequireAuthorization()` to the `MapHub` call, and derive the group from the
authenticated principal rather than the query string:

```csharp
public override async Task OnConnectedAsync()
{
    var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrWhiteSpace(userId))
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    await base.OnConnectedAsync();
}
```

Drop the `?userId=` parameter from `EndorsementSubmissionService.StartAsync` and
`AIAssistantComponent`, and configure `AccessTokenProvider` on the `HubConnectionBuilder` so
the JWT reaches the hub. SignalR removes connections from their groups automatically on
disconnect, so `OnDisconnectedAsync` can lose its body too.

### 3. Report downloads accept caller-supplied object-storage file keys

`BackendAPI/Modules/ATS/Features/DownloadIndividualReport/DownloadIndividualReportHandler.cs`
→ `ReportService.DownloadIndividualReportAsync`

The request carries a list of `{ FileKey, FileName }`. The validator checks only that they
are non-empty and under 255 chars. `ReportService` passes each `FileKey` straight to
`_objectStorageService.DownloadAsync` with no check that the key belongs to a record inside
the caller's access scope.

**Failure scenario.** Any authenticated ATS user — including a `User`-role account scoped to
a single client — posts arbitrary keys and receives a zip of the corresponding objects:
another client's résumés, NBI clearances, government IDs, signed consent forms, or completed
background-check reports. The endpoint is a general-purpose read primitive over the whole
bucket.

**Fix.** Never accept file keys from the client. Change the contract to take
`EmailInvitationRequestId` values, resolve the keys server-side through
`GetDownloadDocumentsAsync`, and apply the access scope to that resolution — i.e. pass
`AtsAccessScope.AuthorizedClientIds` / `RequiredOwnerId` into the query the same way
`GetReportsPageAsync` does. Check `DownloadMultipleOrderRecords` for the same pattern; it
calls `GetDownloadDocumentsAsync` with a caller-supplied id list and no scope filter
(`ReportService.cs:371`).

### 4. `GetReportResult` has no access-scope check

`BackendAPI/Modules/ATS/Services/Report/ReportService.cs:282`

`GetReportsAsync` builds a full access scope (lines 180–210), but
`GetReportResultByEmailInvitationRequestIdAsync` takes an id and queries it directly. The
endpoint has `.RequireAuthorization()`, so the caller is authenticated — but any
authenticated ATS user can read any order's result: subject name, order status, hit status,
package, and every document file name and key.

Note the interaction with finding 3: this endpoint is where an attacker *gets* the file keys
that endpoint 3 will then happily download.

**Fix.** Inject `IAtsAccessScopeResolver`, resolve the scope, and push
`authorizedClientIds` / `requiredRequestorId` into
`GetReportResultByEmailInvitationRequestIdAsync`'s `Where`. Return `NotFoundException` (not
`Forbidden`) when the row exists but falls outside scope, so the endpoint does not confirm
that an id is real.

Audit the other single-record endpoints for the same gap — `GetOrderStatusHistory`,
`MarkAsDisputed` and `ResendApplicationForm` all take an `EmailInvitationID` and should be
checked against the same standard.

---

## Medium

### 5. `GetReportResultByEmailInvitationRequestIdAsync` NREs on unknown id

`ATSRepository.cs:764` dereferences `result!.Educational` before the null check that appears
at `ReportService.cs:299`. `FirstOrDefaultAsync` returns null for an unknown id, so the
null-forgiving `!` turns an intended 404 into an unhandled `NullReferenceException` → 500.
The service's own null check is dead code because the repository throws first.

Move the check ahead of the dereference in the repository and return `null`; keep the
`NotFoundException` in the service. (Same file, line 297: `result!.HitStatus` is read
*before* the `if (result is null)` on line 299 — same bug, one frame up.)

### 6. `ApplicationFormService` holds per-request state in instance fields

`ApplicationFormService.cs:14-28` — fifteen `private string ...Key = ""` fields accumulate
uploaded object keys across the call, and the `catch` block at line 103 uses them to clean
up orphaned uploads.

The service is registered `AddScoped`, so today this is safe: one scope per request, no
concurrent use. But it is fragile in a way that will bite silently. If the service is ever
resolved from a background job scope that processes more than one form, or someone changes
the registration to `AddSingleton` (as `AtsOrderDraftStore` is), the fields leak across
submissions — form B's cleanup would delete form A's files, and form B's entities would be
saved pointing at form A's keys.

Replace the fields with a local `List<string> uploadedKeys` (or a small `UploadedKeys`
record) threaded through the private `Add*Async` methods. That also removes the ordering
coupling where `AddEducationalBackgroundDataAsync` reads keys set by an earlier call.

While in this file: line 355 has a leftover debug local,
`var test = BitConverter.ToString(signatureBytes.Take(16).ToArray());` — dead code, delete it.

### 7. `GetBulkUploadSubjectsEndpoint` blocks on `.Result`

`BackendAPI/Modules/ATS/Features/BulkUploads/Query/GetBulkUploadSubjects/GetBulkUploadSubjectsEndpoint.cs:33`

```csharp
return Results.Ok(new GetBulkUploadSubjectsEndpointResponse(result.Result));
```

If `result` is the `Task`, this is sync-over-async: it blocks a thread-pool thread and
wraps any exception in an `AggregateException`, which `CustomExceptionHandler` will not map
to the right status code. If `result` is a record with a `Result` property, the name is
merely confusing. Either way, `await` it or rename the property — this is the only such
occurrence in the backend, so it reads as an oversight.

### 8. `async void` event handlers

- `UI/FrontendWebassembly/Component/ATS/NewOrderComponent.razor.cs:131` — `OnATSResponse`
- `UI/FrontendWebassembly/Pages/Auth/Otp.razor.cs:191` — `HandleResendOtp`

An exception inside an `async void` cannot be caught by the caller and, in WASM, is lost —
the user sees nothing happen. `OnATSResponse` is invoked from a SignalR callback that
already wraps it in `catch { }` (`EndorsementSubmissionService.cs:43`), so a throw there
vanishes twice over.

Change both to `async Task`. For the event-handler signature, keep `void` at the delegate
boundary but move the body into an `async Task` method and observe it, or change
`ATSResponseReceived` to `Func<string, Task>`. While there, replace the two empty
`catch { }` blocks in `EndorsementSubmissionService.StartAsync` with a `_logger.LogError`.

### 9. `NewOrderComponent` never unsubscribes from the hub event

`NewOrderComponent.razor.cs:25` does
`EndorsementSubmissionService.ATSResponseReceived += OnATSResponse;` but the component
implements neither `IDisposable` nor `IAsyncDisposable`. `EndorsementSubmissionService` is
registered for the app lifetime, so every visit to the New Order page adds another
subscription to a live delegate holding a disposed component.

**Symptom the user will report:** after navigating to New Order and away a few times, one
bulk-upload notification shows up as three, four, five identical snackbars. Eventually
`StateHasChanged` on disposed components throws `ObjectDisposedException` into the SignalR
callback's `catch { }` and notifications stop arriving entirely.

`AIAssistantComponent` and `AIChat` do this correctly — copy their pattern:

```csharp
public void Dispose() => EndorsementSubmissionService.ATSResponseReceived -= OnATSResponse;
```

Also give `EndorsementSubmissionService` an `IAsyncDisposable` that stops and disposes
`_hubConnection`.

### 10. Bulk CSV preview parser does not handle quoted fields

`NewOrderComponent.razor.cs:337` `BuildCsvPreview` splits on `,` and `\n` by hand, while the
backend parses the same file with CsvHelper (`BulkSubmissionProcessorService`).

A row like `"Dela Cruz, Jr.",Juan,,juan@x.com,09171234567` previews as six misaligned
columns and the operator sees garbage — or, worse, approves a preview that does not match
what the backend will actually import. Embedded newlines inside quotes break it further.

Use CsvHelper in the UI too (it is already a dependency of the solution) so preview and
import agree. Bonus: cap the preview at ~100 rows — a 10k-row CSV currently builds a
`List<List<string>>` of every cell in the browser's memory before showing a dialog.

Related, same method: `BuildCsvPreview` reads the file via `OpenReadStream()` with **no
`maxAllowedSize`**, so it uses Blazor's 512 KB default and throws on any larger CSV. The
upload path right below it correctly passes 25 MB
(`EndorsementSubmissionService.cs:133`). A 600 KB CSV therefore fails at preview with an
unhandled exception while being perfectly valid for upload.

### 11. Dashboard loads every invitation row into memory

`ATSRepository.GetDashboardDataAsync` (line 524) does
`.Include(i => i.ReportDetails).ToListAsync()` with no date bound, then
`DashboardService.CreateDashboard` does all grouping, YTD bucketing and counting with LINQ
to Objects.

For a super admin (`authorizedClientIds == null`) this materialises the entire
`EmailInvitationRequest` table plus every related `ReportDetails` row on every dashboard
load. It is fine at current volume and will degrade smoothly right up until it doesn't —
the failure mode is a slow dashboard, then request timeouts, then OOM on the API pod.

Push the aggregation into SQL: the YTD series is a `GROUP BY date_trunc('month', ...)`, the
status tiles are `COUNT(*) FILTER (WHERE ...)`. At minimum, bound the query to the current
year, since `CreateDashboard` discards everything outside `yearStart..yearEnd` anyway.

### 12. Swagger UI is exposed in Production

`BackendAPI/API/APIs/ServiceConfig/AppConfiguration.cs:35-39`

```csharp
if (app.Environment.IsProduction())
{
    await DatabaseExtensions.IntializeDatabaseAsync(app);
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

This publishes a complete map of every endpoint, parameter and DTO to anyone who can reach
the API host — including the anonymous endpoints in finding 1. Note that `Sandbox` and
`UAT` deliberately do *not* enable it, which suggests the Production branch was copied from
Development rather than chosen.

Remove both Swagger calls from the Production branch, or gate them behind an authenticated
policy. Separately, `IntializeDatabaseAsync` running automatically at Production startup
means a migration applies on deploy with no human in the loop — worth a conscious decision
rather than an inherited default.

---

## Low

### 13. Assistant stores grow without bound

`AtsChatHistoryStore` and `AtsOrderDraftStore` are singletons keyed by user id.
`AtsChatHistoryStore` caps each user's history at 20 messages but never removes a user's
entry, and `GetUserLock` adds a `SemaphoreSlim` per user that is only disposed by an
explicit `Clear` call. Over a long-running process this is a slow leak proportional to
distinct users, not concurrent ones. `AtsOrderDraftStore.RemoveExpired` is O(n) over all
drafts on every stage/consume.

Move both to `HybridCache` (already in the stack) with a sliding expiration, or add an
eviction sweep. Also note `RemoveExpired` runs on the calling thread inside the request
path — with a `MemoryCache` you would get expiry for free.

### 14. Duplicated role-ladder logic

`AtsAccessScopeResolver`'s own doc comment records this: the same role ladder is inlined in
`ReportService` (180-210), `EndorsementSubmissionService`, `DisputeOrderService`,
`DashboardService` and `AtsAssistantPlugin`. That was a deliberate, correct call at the time
— behaviour-preserving extraction is its own change.

It is worth doing now, because findings 3 and 4 both trace back to a service that had to
remember to apply the ladder and didn't. Five copies is five chances to forget. Convert them
one service per PR, each with its integration tests green before the next.

### 15. Zero UI test coverage

The backend has ~150 integration tests across ATS; `Test/Test` contains no bUnit project and
no test touching `UI/FrontendWebassembly`. The riskiest UI logic is exactly the kind that
unit-tests well: `BuildCsvPreview`, the `ApplicationFormComponent` draft
save/restore round-trip, and `CanAddEmployer2` / `CanAddEmployer3` (long boolean chains,
easy to get subtly wrong).

Add a bUnit project and start with those three — not a coverage mandate, just the parts
where a silent regression is expensive.

---

## Suggested sequencing

**PR 1 — anonymous surface (do first, ship alone).** Findings 1, 2, 12. These are the
externally reachable ones; 1 and 2 need no credentials at all. Small, self-contained, and
each has a clear test.

**PR 2 — authenticated IDOR.** Findings 3, 4, 5. Same theme: resolve identity server-side,
apply the scope, return 404 on miss. Do 5 alongside since it is in the same method as 4.

**PR 3 — UI correctness.** Findings 8, 9, 10. All user-visible bugs; 9 and 10 are the two a
user is most likely to hit on an ordinary day.

**PR 4 — robustness.** Findings 6, 7, 13.

**PR 5+ — as capacity allows.** Findings 11, 14, 15.

## What is working well

Worth recording so it does not get refactored away by accident:

- **The DB-claim job pattern.** `GetPendingEmailInvitationRequestsAsync` and
  `GetBulkUploadFileDetailsAsync` combine `FOR UPDATE SKIP LOCKED`, per-client round-robin
  fairness via `ROW_NUMBER() PARTITION BY`, a bounded retry count, and a stale-claim
  sweeper. The comments explain *why* rather than *what*. Dropping Redis for this was right.
- **Keyset pagination.** One `CursorCodec`, decode never throws, a bad cursor self-heals to
  page one instead of 400ing. `ApplyReportsSeek`'s NULL-aware predicate correctly mirrors
  Postgres `DESC NULLS FIRST` — that is a subtle thing to get right.
- **Cache-key scoping.** `ATSCacheRepository` folds `ClientScope` and `RequestorScope` into
  every key, so cached pages cannot leak across tenants. Caching only the first page of a
  keyset walk is the right call.
- **The AI assistant's blast radius.** `_kernel.Clone()` per request keeps ATS plugins off
  the shared kernel; drafts are single-use, owner-checked and expiring; the system prompt
  explicitly marks candidate data as data-not-instructions. `StageNewOrder` preparing rather
  than creating is a good human-in-the-loop boundary.
- **Secret hygiene.** `.env` is gitignored and untracked; no credentials in any
  `appsettings.json`.
