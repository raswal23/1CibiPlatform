# ATS + OnePlatform — fix details, item by item

For each of the 15 findings: the code as it was, the code as it is, what was added, and
why that approach over the alternatives.

Companion to [ats-oneplatform-code-review.md](ats-oneplatform-code-review.md) (the
findings) and [ats-oneplatform-code-review-fixes.md](ats-oneplatform-code-review-fixes.md)
(the summary).

Implemented 2026-08-22 against `dev` @ `63758ba8`.
**Build:** 0 errors. **Tests:** 547 passed / 0 failed (was 528).

## Files added

| File | Finding | Purpose |
|---|---|---|
| `BackendAPI/Modules/ATS/DTO/ApplicationFormClaimDTO.cs` | 1 | Server-side view of a token's invitation |
| `BackendAPI/BuildingBlocks/BuildingBlocks/Exceptions/ConflictException.cs` | 1 | 409 for spent/terminal resources |
| `BackendAPI/BuildingBlocks/BuildingBlocks/SignalR/HubCallerContextExtensions.cs` | 2 | Group name from the validated principal |
| `UI/FrontendWebassembly/SharedService/CsvPreviewParser.cs` | 10 | RFC 4180 preview parser |
| `Test/Test/UI/CsvPreviewParserTests.cs` | 15 | First UI test suite |

---

# 1. Anonymous form POST trusted a body-supplied ID

**Critical — unauthenticated write access to PII.**

## What was already right

Your hash-token design was in place and correct in three places. This matters because the
fix reuses it rather than replacing it:

```csharp
// ATSRepository — expiry checked properly
public async Task<bool> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken)
{
    return await _dbcontext.EmailInvitationRequests
        .AsNoTracking()
        .AnyAsync(eir => eir.HashToken == hashToken &&
                  eir.HashTokenExpiration > DateTime.UtcNow, cancellationToken);
}
```

Used by PhilSys (`PartnerSystemService.cs:43`). The form *load* was gated, and the client
blocked rendering on all three terminal conditions:

```csharp
// ATSApplicationForm.razor.cs — your client gate
var response = await ATSService.GetEmailIdAndApplicationFormPathAsync(HashToken!);
Status = details.Status;
IsExpired = details.IsExpired;

private bool IsInstructionsVisible =>
    !_showApplicationForm && !IsExpired &&
    !string.Equals(Status, "Done", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(Status, "Withdrawn", StringComparison.OrdinalIgnoreCase);
```

`WithdrawnApplicationForm` was token-authorized end to end.

## The actual gap

**The token gated the read, not the write.** At HEAD the submit endpoint had no token at
all:

```csharp
// BEFORE — AddApplicationFormDataEndpoint.cs
public record AddApplicationFormDataRequest(PersonalDetailsDTO PersonalDetails,
                                            AddressDetailsDTO AddressDetails,
                                            /* ...five more DTOs... */
                                            SignatureDetailsDTO SignatureDetails);
// ^ no HashToken field — the token never reached the server on POST
```

Identity came from the body:

```csharp
// BEFORE — ApplicationFormService, identity from a client-supplied Guid
Identity = personalDetails.EmailInvitationID,
// ...
await _atsRepository.UpdateEmailInvitationRequestForFilledUpFormAsync(personalDetails.EmailInvitationID);
```

Your client gate runs in Blazor WASM and decides what to **render**. A caller issuing the
POST directly never runs `OnInitializedAsync`, so there was nothing to bypass.

Guid v7 is not a mitigation — timestamp-derived, and `getemailidandapplicationformpath`
hands it to the browser anyway.

## What was added

**New DTO** — carries only what the authorization decision needs:

```csharp
// ADDED: BackendAPI/Modules/ATS/DTO/ApplicationFormClaimDTO.cs
public record ApplicationFormClaimDTO
{
    public Guid EmailInvitationID { get; init; }
    public DateTime? HashTokenExpiration { get; init; }
    public string? ApplicationFormStatus { get; init; }

    public bool IsExpired => !HashTokenExpiration.HasValue
        || HashTokenExpiration.Value <= DateTime.UtcNow;
}
```

**New repository method** — deliberately uncached:

```csharp
// ADDED: ATSRepository.cs
// Deliberately not cached and not decorated: this is the authorization decision for
// the anonymous application-form endpoints, so a stale expiry or form status would
// keep a spent link working.
public async Task<ApplicationFormClaimDTO?> GetApplicationFormClaimAsync(string hashToken,
                    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(hashToken))
        return null;

    return await _dbcontext.EmailInvitationRequests
            .AsNoTracking()
            .Where(eir => eir.HashToken == hashToken)
            .Select(eir => new ApplicationFormClaimDTO
            {
                EmailInvitationID = eir.EmailInvitationID,
                HashTokenExpiration = eir.HashTokenExpiration,
                ApplicationFormStatus = eir.ApplicationFormStatus
            })
            .FirstOrDefaultAsync(cancellationToken);
}
```

**Why uncached:** every other read on this repository is decorated by
`ATSCacheRepository`. This one is a pass-through by design — a cached claim would let a
withdrawn invitation keep accepting posts until the tag happened to be evicted.

```csharp
// ADDED: ATSCacheRepository.cs
// Pass-through by design. The claim drives an authorization decision, so caching it
// would let a withdrawn or already-submitted invitation keep accepting form posts
// until the tag happened to be evicted.
public async Task<ApplicationFormClaimDTO?> GetApplicationFormClaimAsync(string hashToken, CancellationToken cancellationToken)
{
    return await _atsRepository.GetApplicationFormClaimAsync(hashToken, cancellationToken);
}
```

**New authorization method** — modelled on your `WithdrawnApplicationForm`:

```csharp
// ADDED: ApplicationFormService.cs
/// <summary>
/// Resolves the invitation a hash token refers to, rejecting unknown, expired and
/// already-spent tokens. Returns the id every child record must be written against.
/// </summary>
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

**The body value is overwritten, not compared:**

```csharp
// CHANGED: ApplicationFormService.AddApplicationFormDataAsync
// The invitation comes from the emailed token, never from the request body. A
// caller who guesses another candidate's EmailInvitationID gets nowhere because
// the value they sent is overwritten below.
var emailInvitationId = await AuthorizeApplicationFormAsync(hashToken, ct);

personalDetails.EmailInvitationID = emailInvitationId;
addressDetails.EmailInvitationID = emailInvitationId;
educationalBackground.EmailInvitationID = emailInvitationId;
licensesDetails.EmailInvitationID = emailInvitationId;
professionalExperiences.EmailInvitationID = emailInvitationId;
referenceDetails.EmailInvitationID = emailInvitationId;
signatureDetails.EmailInvitationID = emailInvitationId;
```

**Why overwrite rather than compare?** Comparing and rejecting on mismatch still leaks
information: a caller could vary the body id and learn which ones pair with their token.
Overwriting makes the field inert — it cannot influence anything.

**Exception passthrough** — rejected tokens are the caller's problem, not a server fault:

```csharp
// CHANGED: the catch block no longer flattens 4xx to 500
if (ex is NotFoundException or BadRequestException or ConflictException)
    throw;

throw new InternalServerException($"Failed to add transaction. {ex.InnerException?.Message ?? ex.Message}");
```

**New exception + handler arm:**

```csharp
// ADDED: BuildingBlocks/Exceptions/ConflictException.cs
/// <summary>
/// The request was understood and authorized, but the resource is in a state that
/// forbids it - a form already submitted, an invitation already withdrawn. Maps to 409
/// so callers can tell "you may not" apart from "not any more".
/// </summary>
public class ConflictException : Exception { /* ... */ }
```

```csharp
// ADDED: CustomExceptionHandler.cs
ConflictException =>
(
    exception.Message,
    exception.GetType().Name,
    context.Response.StatusCode = StatusCodes.Status409Conflict
),
```

**Also fixed — expired tokens on the load path.** Your `IsHashTokenValidAsync` checked
expiry, but `GetEmailIdAndApplicationFormPathAsync` filters on `HashToken` alone and
never called it, so a dead link still returned a usable `EmailId`:

```csharp
// ADDED: ApplicationFormService.GetEmailIdAndApplicationFormPathAsync
// The lookup query filters on HashToken alone, so an expired link would otherwise
// still hand back a usable EmailInvitationID.
if (!emailIdAndApplicationFormPath.ExpiresAt.HasValue
    || emailIdAndApplicationFormPath.ExpiresAt.Value <= DateTime.UtcNow)
{
    _logger.LogWarning("Rejected an expired application form link: {@Context}", logContext);
    throw new BadRequestException("This application form link has expired. Please request a new one.");
}
```

**UI side** — the component already held `HashToken` as a `[Parameter]`, so it just had
to travel with the POST:

```csharp
// ADDED: UI ApplicationFormService.cs
// The token the candidate arrived with. This is what the server authorizes
// against - the EmailInvitationID fields below are ignored server-side.
AddString(HashToken, "HashToken");
```

```csharp
// CHANGED: ApplicationFormComponent.razor.cs:895
var response = await ATSService.AddApplicationFormDataAsync(HashToken!, personalDetails, /* ... */);
```

**Validator** — shape only, since the real check needs the database:

```csharp
// ADDED: AddApplicationFormDataHandler.cs
// Shape only. Whether the token is real, unexpired and unspent is decided
// against the database in ApplicationFormService.AuthorizeApplicationFormAsync.
RuleFor(x => x.HashToken)
    .NotEmpty().WithMessage("Application form token is required.");
```

**Your client gate stays.** It gives the candidate a proper "link expired" screen instead
of a raw 400. This is defence in depth, not a replacement.

## Rate limiting — through your gateway, not a second limiter

I initially added `AddRateLimiter` to the API host. You corrected that, and it was
removed. The policy now goes through `RouteDefinitionDTO.Metadata`, matching
`AuthPaths.cs`:

```csharp
// ADDED: GatewayConstants.RateLimitPolicies
/// <summary>
/// The candidate-facing ATS application form: token lookup, submission and
/// withdrawal. These accept unauthenticated callers, so they are not bounded by
/// the login flow the way the rest of the platform is.
/// </summary>
public const string AnonymousApplicationForm = "AnonymousApplicationForm";
```

```csharp
// ADDED: GatewayServiceExtensions.cs
// Partitioned by client IP rather than by policy name: these routes are
// anonymous, so a single shared bucket would let one caller starve every
// candidate filling in a form. A candidate loads the form once and
// submits once; 30/min leaves room for retries and shared office NAT
// while making EmailInvitationID enumeration impractical.
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

Note the partition key differs from `LoginPolicy`, which partitions on the policy name
(one global bucket). For anonymous routes that would be a self-inflicted DoS.

Applied to all three anonymous routes in `ATSPaths.cs`:

```csharp
// ADDED to AddApplicationFormDataEntryPoint, GetEmailIdandApplicationFormPathEntryPoint,
// and WithdrawnApplicationForm
Metadata: new Dictionary<string,string>
{
    { "RateLimitPolicy", GatewayConstants.RateLimitPolicies.AnonymousApplicationForm }
}
```

Endpoints also gained explicit `.AllowAnonymous()` — previously they were anonymous by
omission, which reads as an oversight rather than a decision.

## Tests: +6

```csharp
[Fact]
public async Task AddApplicationFormData_WithMismatchedEmailInvitationId_ShouldBindToTokenOwner()
{
    // The heart of the finding: a caller posts a valid token of their own but
    // substitutes somebody else's EmailInvitationID in the body. The body value must
    // be ignored entirely.
    await SeedEmailInvitationRequestData();

    var victimEmailId = Guid.CreateVersion7();
    var command = BuildValidCommand(SeededHashToken, claimedEmailId: victimEmailId);

    var result = await _sender.Send(command);

    result.IsAdded.Should().BeTrue();

    // Written against the token's own invitation...
    _dbContext.PersonalDetails.Any(p => p.EmailInvitationID == EmailId).Should().BeTrue();

    // ...and not against the id the caller asked for.
    _dbContext.PersonalDetails.Any(p => p.EmailInvitationID == victimEmailId).Should().BeFalse();
}
```

Plus: unknown token (404 + nothing written), empty token (validation), expired token (400
+ nothing written), already-submitted (409), withdrawn (409).

---

# 2. SignalR hub joined any group named in the query string

**High — no credentials required.**

## Before

```csharp
// BEFORE — ATSHub.cs
public class ATSHub : Hub<IATSClient>   // no [Authorize]
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        await base.OnConnectedAsync();
    }
    // ...OnDisconnectedAsync did the mirror image
}
```

`MapHub<ATSHub>` had no `.RequireAuthorization()`. Connect to
`/hubs/atsbulk?userId=<victim-guid>` and you receive their bulk-upload notifications and
AI-assistant responses — which stream candidate names and order statuses to that group.

`AIAgentHub` was identical, and additionally dropped `AddToGroupAsync` unawaited
(`Groups.AddToGroupAsync(...)` with no `await`), so a client could be sent its first
message before it finished joining.

## Why not inject `ICurrentUser`

You pointed out `ICurrentUser` already extracts JWT claims. It does — but:

```csharp
// CurrentUser.cs — scoped, backed by IHttpContextAccessor
private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
```

`HttpContext` is null for hub *invocations* after the handshake completes, so
`ICurrentUser` would intermittently return null inside a hub. The hub reads
`Context.User` — the same validated principal, delivered by SignalR — through a helper
that mirrors `CurrentUser`'s claim order exactly:

```csharp
// ADDED: BuildingBlocks/SignalR/HubCallerContextExtensions.cs
public static class HubCallerContextExtensions
{
    // The same pair CurrentUser reads, in the same order, so a hub group and an
    // ICurrentUser.UserId always agree on who the caller is.
    private const string UserIdClaim = "userId";

    /// <remarks>
    /// Derived from the validated token only. Never fall back to a query-string value:
    /// the group decides who receives another user's notifications.
    /// </remarks>
    public static string? GetUserGroupName(this HubCallerContext context)
    {
        var value = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(value))
            value = context.User?.FindFirst(UserIdClaim)?.Value;

        // Round-trip through Guid so the group name is canonically formatted regardless
        // of how the claim was written.
        return Guid.TryParse(value, out var userId) && userId != Guid.Empty
            ? userId.ToString()
            : null;
    }
}
```

**Why a shared helper rather than duplicating in each hub:** two hubs had the same bug.
One place to get it right.

## After

```csharp
// CHANGED: both ATSHub and AIAgentHub
[Authorize]
public class ATSHub : Hub<IATSClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetUserGroupName();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    // SignalR removes a connection from its groups when it disconnects, so there is
    // nothing to undo on the way out.
}
```

`OnDisconnectedAsync` deleted from both — SignalR handles group removal automatically, so
the override was pure risk (it re-read the query string).

**No `AccessTokenProvider` needed.** The JWT arrives via the HttpOnly auth cookie, which
the browser sends on the WebSocket handshake:

```csharp
// ServiceConfiguration.cs — existing, unchanged
OnMessageReceived = context =>
{
    if (context.Request.Cookies.TryGetValue(_httpCookieOnlyKey!, out var token))
        context.Token = token;
    return Task.CompletedTask;
}
```

## Client side

```csharp
// CHANGED: EndorsementSubmissionService.StartAsync
// BEFORE:
var userId = await _localStorageService.GetItemAsync<string?>(_userIdKey) ?? Guid.CreateVersion7().ToString();
var hubUrl = $"{baseUri}/hubs/atsbulk?userId={userId}";

// AFTER:
// No ?userId= any more. The hub derives the group from the authenticated
// principal; the auth cookie rides along on the handshake automatically.
var hubUrl = $"{baseUri}/hubs/atsbulk";
```

Note the `Guid.CreateVersion7()` fallback in the old code — when local storage was empty
the client invented an identity. Same change applied to `AIChatService`.

The two empty `catch { }` blocks in the hub callbacks now log:

```csharp
// CHANGED
catch (Exception ex)
{
    // A subscriber that throws must not tear down the hub connection, but
    // swallowing it silently is how duplicate-notification bugs stay hidden.
    _logger.LogError(ex, "An ATS hub subscriber threw while handling a response.");
}
```

---

# 3. Report downloads accepted caller-supplied storage keys

**High.**

## Before

```csharp
// BEFORE — the wire contract
public class DownloadIndividualDocumentsRequestDTO
{
    public List<DownloadIndividualDocuments> FileDocuments { get; set; } = [];
    public string? SubjectName { get; set; }
}
public class DownloadIndividualDocuments
{
    public string? FileKey { get; set; }   // <-- straight from the browser
    public string? FileName { get; set; }
}
```

```csharp
// BEFORE — ReportService, key handed to the bucket unchecked
foreach (var file in downloadInvididualRequest.FileDocuments)
{
    var entry = archive.CreateEntry(file.FileName ?? "UnknownFile");
    await using var ossStream = await _objectStorageService.DownloadAsync(file.FileKey!, cancellationToken);
    await ossStream.CopyToAsync(entryStream, cancellationToken);
}
```

The validator only checked non-empty and ≤255 chars. Any authenticated user could name any
object in the bucket.

## Why change the contract instead of validating keys

Validating a key means answering "does this key belong to an order this caller may see?" —
which requires the order lookup anyway. Once you have the order, the key is derivable, so
accepting it from the client buys nothing. The UI was already selecting document *types*
(checkboxes), so types were the natural wire format:

```csharp
// AFTER: BackendAPI/Modules/ATS/DTO/DownloadIndividualDocumentsRequestDTO.cs
/// <remarks>
/// Carries the order id and which kinds of document to include - never object storage
/// keys. The previous shape accepted caller-supplied FileKey values and passed them
/// straight to object storage, which made this endpoint a general-purpose read over the
/// whole bucket for any authenticated user. The server now resolves keys itself, under
/// the caller's access scope.
/// </remarks>
public class DownloadIndividualDocumentsRequestDTO
{
    public Guid EmailInvitationRequestId { get; set; }
    public List<string> DocumentTypes { get; set; } = [];
}

public static class AtsDocumentTypes
{
    public const string BiometricPhoto = "BiometricPhoto";
    public const string Resume = "Resume";
    public const string GovernmentId = "GovernmentId";
    public const string Diploma = "Diploma";
    public const string Coe = "Coe";
    public const string ConsentForm = "ConsentForm";
    public const string Report = "Report";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { BiometricPhoto, Resume, GovernmentId, Diploma, Coe, ConsentForm, Report };
}
```

## Service

```csharp
// AFTER: scope → order → keys
if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
    throw new NotFoundException($"No documents found for email invitation ID {...}.");

var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(
    downloadInvididualRequest.EmailInvitationRequestId,
    scope.AuthorizedClientIds,
    scope.RequiredOwnerId,
    cancellationToken);

if (result is null)
{
    _logger.LogWarning("No documents in scope for the caller {@Context}", logContext);
    throw new NotFoundException($"No documents found for email invitation ID {...}.");
}

var files = ResolveRequestedDocuments(result, requested).ToList();
```

**New mapping method** — an iterator so unavailable types are skipped rather than
producing empty zip entries:

```csharp
// ADDED
/// <summary>
/// Maps the requested document type names onto the (file name, file key) pairs the
/// order actually carries. Types with no stored document are skipped.
/// </summary>
private static IEnumerable<(string FileName, string FileKey)> ResolveRequestedDocuments(
    ReportResultDTO result,
    IReadOnlySet<string> requested)
{
    var candidates = new (string Type, string? FileName, string? FileKey)[]
    {
        (AtsDocumentTypes.BiometricPhoto, result.BiometricPhotoFileName, result.BiometricPhotoFileKey),
        (AtsDocumentTypes.Resume, result.ResumeFileName, result.ResumeFileKey),
        (AtsDocumentTypes.GovernmentId, result.IdUploadedFileName, result.IdUploadedFileKey),
        (AtsDocumentTypes.Diploma, result.DiplomaFileName, result.DiplomaFileKey),
        (AtsDocumentTypes.Coe, result.CoeFileName, result.CoeFileKey),
        (AtsDocumentTypes.ConsentForm, result.ConsentFormFileName, result.ConsentFormFileKey),
        (AtsDocumentTypes.Report, result.UploadedReportFileName, result.UploadedReportFileKey),
    };

    foreach (var (type, fileName, fileKey) in candidates)
    {
        if (requested.Contains(type)
            && !string.IsNullOrWhiteSpace(fileName)
            && !string.IsNullOrWhiteSpace(fileKey))
        {
            yield return (fileName, fileKey);
        }
    }
}
```

**The zip filename also came from the client** (`request.downloadInvididualRequest.SubjectName`).
The service now returns it, so the endpoint names the file from the order the caller was
actually allowed to read:

```csharp
// CHANGED: IReportService
/// <summary>
/// Zips the requested documents for one order. The subject name comes back with the
/// stream so the endpoint can name the file without trusting caller input.
/// </summary>
Task<(Stream ZipStream, string SubjectName)> DownloadIndividualReportAsync(...);
```

**Repository scope** — applied *inside* the query, so an out-of-scope id yields no rows
rather than being checked afterwards:

```csharp
// ADDED: ATSRepository.GetDownloadDocumentsAsync
// The scope predicates are applied inside the query rather than checked afterwards,
// so an id outside the caller's scope simply yields no rows - the caller cannot tell
// an unauthorized order from a non-existent one.
.Where(eir => (authorizedClientIds == null
        || (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
    && (!requiredRequestorId.HasValue
        || eir.RequestorId == requiredRequestorId.Value))
```

**Validator** — unknown type names are rejected loudly:

```csharp
// ADDED
// Reject unknown type names outright rather than silently returning a short
// zip - a typo in the UI should be loud.
RuleForEach(x => x.downloadInvididualRequest.DocumentTypes)
    .Must(AtsDocumentTypes.All.Contains)
    .WithMessage("Unknown document type.");
```

**UI** — 50 lines of key-copying became 7 lines of type names:

```csharp
// AFTER: SelectFilesToDownloadComponent.razor.cs
// Send which kinds of document we want; the server resolves the storage keys
// itself, under the caller's access scope. It used to accept keys from here,
// which meant the browser could name any object in the bucket.
DownloadRequest.EmailInvitationRequestId = EmailInvitationId;
DownloadRequest.DocumentTypes.Clear();

if (BiometricPhotoSelected) DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.BiometricPhoto);
if (ResumeSelected)         DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Resume);
// ...
```

`DownloadMultipleOrderRecordsAsync` had the same shape and got the same scope treatment.

## Tests: +3

```csharp
[Fact]
public async Task DownloadIndividualReportAsync_ShouldThrowNotFound_WhenOrderBelongsToAnotherClient()
{
    // The finding this endpoint had: a caller scoped to one client could name any
    // order and receive its documents.
    // ...seed an order under ClientId = 2...
    SetAuthenticatedUser(Guid.CreateVersion7(), AtsRoleIds.User, claimedClientId: 1);

    await Assert.ThrowsAsync<NotFoundException>(() =>
        _reportService.DownloadIndividualReportAsync(request, CancellationToken.None));
}
```

Plus a unit test asserting object storage is **never touched** when the order is
out of scope:

```csharp
_objectStorage.Verify(
    storage => storage.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
    Times.Never);
```

---

# 4. `GetReportResult` had no access-scope check

**High.** Also the endpoint that supplied the keys for finding 3.

`GetReportsAsync` built a full scope; this method took an id and queried it directly.

```csharp
// BEFORE
var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(emailInvitationRequestId, cancellationToken);
```

```csharp
// AFTER
// Any authenticated ATS user could previously read any order's result - subject
// name, hit status, and every document key - which was also how a caller
// obtained the keys the download endpoint used to accept.
if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
    throw new NotFoundException($"No report result found for email invitation ID {emailInvitationRequestId}.");

var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(
    emailInvitationRequestId,
    scope.AuthorizedClientIds,
    scope.RequiredOwnerId,
    cancellationToken);

// NotFound rather than Forbidden on purpose: a caller must not be able to probe
// which order ids exist outside their scope.
if (result is null)
{
    _logger.LogWarning("No report result in scope for the caller {@Context}", logContext);
    throw new NotFoundException($"No report result found for email invitation ID {emailInvitationRequestId}.");
}
```

**Why 404 and not 403:** a 403 confirms the id exists. With 404 both cases are
indistinguishable.

**The cache key had to change too** — this is easy to miss and would have silently undone
the fix:

```csharp
// BEFORE
var cacheKey = $"report_result_{emailInvitationRequestId}";

// AFTER
// The scope is part of the key. Without it the first caller to read an order
// would populate an entry that every other caller then shares, which would put
// the access check back where it started.
var cacheKey = $"report_result_{emailInvitationRequestId}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";
```

`ClientScope` / `RequestorScope` are your existing helpers — the same ones the keyset
pagination keys already use.

---

# 5. NRE on unknown report id

```csharp
// BEFORE — ATSRepository.cs:764, dereference before the null check
string? diplomaFileName = result!.Educational?.DoctorateDiplomaFileName ?? ...;
```

```csharp
// BEFORE — ReportService.cs, the null check that could never run
if (string.IsNullOrWhiteSpace(result!.HitStatus))   // <-- throws here first
    result.HitStatus = "-";

if (result is null)                                  // <-- dead code
    throw new NotFoundException(...);
```

`FirstOrDefaultAsync` returns null for an unknown id, so `result!` threw an
unhandled `NullReferenceException` → 500, and the service's own 404 was unreachable.

```csharp
// AFTER — ATSRepository.cs
// An unknown id - or one outside the caller's scope - returns null here. The
// null-forgiving dereference below used to turn that into a 500 before the
// service's own null check could run.
if (result is null)
    return null;

string? diplomaFileName = result.Educational?.DoctorateDiplomaFileName ?? ...;
```

In the service, the null check moved **above** the `HitStatus` normalisation (see
finding 4's snippet). Both bugs were the same mistake one frame apart.

---

# 6. Per-request state in service instance fields

```csharp
// BEFORE — 15 fields on a scoped service
private string resumeFileKey = "";
private string nbiKey = "";
private string govtIdKey = "";
private string biometricFileKey = "";
private string highSchoolDiplomaKey = "";
// ...10 more
```

Used to clean up orphaned uploads on rollback:

```csharp
// BEFORE
var keys = new[] { resumeFileKey, nbiKey, govtIdKey, /* ...11 more... */ };
foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
    await _objectStorageService.DeleteAsync(key, ct);
```

Safe today — `AddScoped`, one scope per request. But `AtsOrderDraftStore` in the same
module is `AddSingleton`, so the registration style is not uniform, and a background job
scope processing two forms would corrupt both.

```csharp
// AFTER
/// <summary>
/// Object keys uploaded during a single submission. Local to the call rather than a
/// field on the service, so a second submission can never delete or reuse the first
/// one's uploads regardless of how the service is scoped.
/// </summary>
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

**Why `Track` returns the key:** it wraps the upload call inline, so the local variable
and the tracking cannot drift apart:

```csharp
// AFTER — call site
resumeFileKey = uploadedKeys.Track(
    await _objectStorageService.UploadAsync(_folderName, personalDetailsDTO.ResumeFileName!, resumeStream, cancellationToken));
```

Rollback is now:

```csharp
foreach (var key in uploadedKeys.All)
{
    try { await _objectStorageService.DeleteAsync(key, ct); }
    catch (Exception deleteEx) { _logger.LogWarning(deleteEx, "Failed to delete file with key {Key}", key); }
}
```

This also removed an ordering coupling: `AddEducationalBackgroundDataAsync` previously
read fields set by `AddPersonalDetailsDataAsync`. Each method now declares its own locals.

**Also deleted** — dead debug code at line 355:

```csharp
// REMOVED
var test = BitConverter.ToString(signatureBytes.Take(16).ToArray());
```

---

# 7. `result.Result` read as sync-over-async

**Investigated — not a bug.**

```csharp
// GetBulkUploadSubjectsHandler.cs
public record GetBulkUploadSubjectsQueryResult(BulkUploadSubjectsResultDTO Result);
```

`result` is the awaited record; `.Result` is its DTO property, not `Task.Result`. No
thread blocks. But it reads exactly like one, which is why it was flagged in review:

```csharp
// AFTER — renamed only, no behaviour change
// Named queryResult rather than result: `result.Result` reads like a
// sync-over-async block on a Task, and it is not one - the send is awaited
// above and Result is just the DTO property.
var queryResult = await sender.Send(query, cancellationToken);

return Results.Ok(new GetBulkUploadSubjectsEndpointResponse(queryResult.Result));
```

---

# 8. `async void` event handlers

Two cases, two different fixes because the call sites differ.

**`Otp.HandleResendOtp`** — bound to `OnClick`, which accepts `Task`:

```csharp
// BEFORE
private async void HandleResendOtp()

// AFTER
// async Task, not async void: OnClick awaits the returned task, so a throw here is
// observed by the framework instead of being lost.
private async Task HandleResendOtp()
```

**`NewOrderComponent.OnATSResponse`** — bound to `event Action<string>`, so it *must*
stay void at the delegate boundary:

```csharp
// BEFORE
private async void OnATSResponse(string message)
{
    await InvokeAsync(() => { Snackbar.Add(message, Severity.Success); StateHasChanged(); });
}
```

```csharp
// AFTER
// The hub event is a plain Action, so the handler has to be void at the delegate
// boundary. Keep the body in an async Task and observe it here rather than letting
// an `async void` throw into the SignalR callback, where nothing can catch it.
private void OnATSResponse(string message)
{
    _ = OnATSResponseAsync(message);
}

private async Task OnATSResponseAsync(string message)
{
    try
    {
        await InvokeAsync(() =>
        {
            Snackbar.Add(message, Severity.Success);
            StateHasChanged();
        });
    }
    catch (ObjectDisposedException)
    {
        // The component went away between the notification arriving and the render.
        // Nothing to show, and nothing worth logging.
    }
}
```

**Why catch `ObjectDisposedException` specifically:** it's the expected race (notification
in flight while the user navigates away), not a defect. Anything else propagates.

---

# 9. `NewOrderComponent` never unsubscribed

```csharp
// NewOrderComponent.razor.cs:25 — subscribed
EndorsementSubmissionService.ATSResponseReceived += OnATSResponse;
```

...with no `Dispose`. The service is registered for the app lifetime, so every visit to
the page added another subscription to a delegate holding a disposed component.

**User-visible symptom:** one bulk-upload notification appearing 3, 4, 5 times, then none
at all once the disposed components started throwing into the SignalR callback's
`catch { }`.

```razor
@* ADDED: NewOrderComponent.razor *@
@inherits CrudPageBase
@implements IDisposable
```

```csharp
// ADDED: NewOrderComponent.razor.cs
public void Dispose()
{
    // EndorsementSubmissionService lives for the lifetime of the app, so without
    // this every visit to this page left another subscription behind - the user saw
    // one notification per visit, and eventually none at all once the disposed
    // components started throwing.
    EndorsementSubmissionService.ATSResponseReceived -= OnATSResponse;
}
```

**Service-level leak too** — the hub connection was never disposed:

```csharp
// CHANGED: IEndorsementSubmissionService
public interface IEndorsementSubmissionService : IAsyncDisposable
```

```csharp
// ADDED: EndorsementSubmissionService
public async ValueTask DisposeAsync()
{
    if (_hubConnection is not null)
    {
        await _hubConnection.DisposeAsync();
        _hubConnection = null;
    }
}
```

`AIAssistantComponent` and `AIChat` already did this correctly — this brings
`NewOrderComponent` in line with the existing pattern rather than inventing one.

---

# 10. CSV preview mis-parsed quoted fields

## Three bugs in one method

```csharp
// BEFORE — NewOrderComponent.BuildCsvPreview
using var stream = bulkUploadFileDetailsDTO.BulkFile!.OpenReadStream();   // (2) no size limit
// ...
var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

result.Headers = lines[0].Split(',').Select(x => x.Trim()).ToList();      // (1) naive split

foreach (var line in lines.Skip(1))                                       // (3) unbounded
    result.Rows.Add(line.Split(',').Select(x => x.Trim()).ToList());
```

1. **Quoted fields.** `"Dela Cruz, Jr.",Juan,...` previewed as six misaligned columns.
   The backend parses the same file with CsvHelper, so the operator approved a preview
   that did not match the import.
2. **512 KB ceiling.** `OpenReadStream()` with no argument uses Blazor's default. The
   upload path immediately below passes 25 MB — so a 600 KB CSV threw at preview while
   being perfectly uploadable.
3. **Unbounded.** A 10k-row CSV built a `List<List<string>>` of every cell in the
   browser before the dialog opened.

## Why not just add CsvHelper to the UI

```
BackendAPI/Modules/ATS/ATS.csproj:  <PackageReference Include="CsvHelper" Version="33.1.0" />
UI/FrontendWebassembly:              not referenced
```

Adding it to a WASM project grows the download payload for every user, to parse a preview.
A single-pass RFC 4180 scan is ~60 lines:

```csharp
// ADDED: UI/FrontendWebassembly/SharedService/CsvPreviewParser.cs
/// <remarks>
/// RFC 4180: fields may be quoted, quoted fields may contain commas, newlines and
/// escaped quotes (""). The preview used to split on ',' and '\n' by hand, so a row like
/// <c>"Dela Cruz, Jr.",Juan,...</c> previewed misaligned - and the operator was
/// approving a preview that did not match what CsvHelper would import on the server.
/// </remarks>
public static class CsvPreviewParser
{
    public const int MaxPreviewRows = 100;

    private static List<List<string>> ParseRecords(string content)
    {
        var records = new List<List<string>>();
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    // "" inside a quoted field is an escaped quote, not the end of it.
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        currentField.Append('"');
                        index++;
                    }
                    else inQuotes = false;
                }
                else currentField.Append(character);   // newlines inside quotes belong to the field
                continue;
            }

            switch (character)
            {
                case '"': inQuotes = true; break;
                case ',': currentRecord.Add(currentField.ToString()); currentField.Clear(); break;
                case '\r': break;   // swallow CR; the LF that follows ends the record
                case '\n':
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = [];
                    break;
                default: currentField.Append(character); break;
            }
        }

        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            records.Add(currentRecord);
        }

        return records;
    }
}
```

Result type reports the total even when truncated:

```csharp
public sealed class CsvPreviewResult
{
    public List<string> Headers { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];

    /// <summary>Total data rows in the file, even when more than MaxPreviewRows.</summary>
    public int TotalRowCount { get; set; }

    public bool IsTruncated => TotalRowCount > Rows.Count;
}
```

Caller shrank from 30 lines to 8:

```csharp
// AFTER
private async Task<CsvPreviewParser.CsvPreviewResult> BuildCsvPreview()
{
    // The 25 MB ceiling matches what InsertBulkSubjectAsync uploads. Without an
    // explicit limit this used Blazor's 512 KB default and threw on any larger
    // file - so a 600 KB CSV failed at preview while being perfectly uploadable.
    using var stream = bulkUploadFileDetailsDTO.BulkFile!
        .OpenReadStream(maxAllowedSize: 25 * 1024 * 1024);

    using var reader = new StreamReader(stream);
    var csvContent = await reader.ReadToEndAsync();

    // Quote-aware, so the preview matches what CsvHelper parses server-side.
    return CsvPreviewParser.Parse(csvContent);
}
```

**The cap is stated, not silent** — a truncated preview that looks complete is worse than
no preview:

```csharp
// ADDED
// Say so when the preview is a sample - a silent cap reads as "this is the whole
// file", and the operator is approving an import on the strength of it.
var previewMessage = previewData.IsTruncated
    ? $"Showing the first {previewData.Rows.Count} of {previewData.TotalRowCount} rows. All rows will be uploaded."
    : "Upload has been disabled. Blank detail is not allowed.";
```

## Tests: +10

Headers/rows, commas inside quotes, `""` escapes, newlines inside quotes, CRLF, blank-row
handling, empty input, headers-only, the cap with `IsTruncated`, whitespace trimming.

---

# 11. Dashboard loaded every invitation row

```csharp
// BEFORE — no date bound at all
return await invitations
    .Include(invitation => invitation.ReportDetails)
    .ToListAsync(cancellationToken);
```

For a super admin (`authorizedClientIds == null`) this materialised the entire
`EmailInvitationRequest` table plus every related `ReportDetails` row, on every dashboard
load. `CreateDashboard` then discarded everything outside the current year.

```csharp
// AFTER — ATSRepository
// Bounded by date. DashboardService discards everything outside the current year
// (YTD series) and the trailing turnaround window anyway, so pulling the whole
// table - which for a platform super admin was every invitation plus every related
// ReportDetails row, on every dashboard load - bought nothing.

// Keep rows with no OrderCreatedAt: the candidate-response tiles count
// invitations by EmailSentStatus, which does not depend on the order date.
invitations = invitations.Where(invitation =>
    !invitation.OrderCreatedAt.HasValue
    || invitation.OrderCreatedAt.Value >= windowStart);
```

**The null-date carve-out matters** — without it the "Not Started" tile would silently
drop invitations that were sent but never ordered.

```csharp
// ADDED: DashboardService
// Everything the dashboard renders lives inside the current year (the YTD series)
// or the trailing 7-day turnaround window. Loading a year is generous for both and
// keeps the query bounded as the table grows.
private static readonly int DashboardWindowMonths = 12;

// Whichever is earlier: the start of this calendar year, or 12 months back. In
// January the rolling window is the wider of the two.
var windowStart = yearStart < now.AddMonths(-DashboardWindowMonths)
    ? yearStart
    : now.AddMonths(-DashboardWindowMonths);
```

**`RecentOrders` was worse.** It projected *every* invitation in scope into a DTO and
shipped it — to a UI with no consumer (`grep` finds `RecentOrders` only in the DTO
definition):

```csharp
// ADDED
/// <summary>How many orders the "recent orders" panel carries.</summary>
private const int RecentOrderCount = 25;

// "Recent" means recent - this used to project and serialise every invitation
// in scope on every dashboard load.
var recentOrders = invitations
    .OrderByDescending(invitation => invitation.OrderCreatedAt)
    .ThenByDescending(invitation => invitation.EmailInvitationID)
    .Take(RecentOrderCount)
    .Select(...)
```

**Not done:** pushing the aggregation into SQL (`GROUP BY date_trunc`,
`COUNT(*) FILTER`). The query is bounded now, which removes the urgency. Noted as a
follow-up.

## Tests: +2

```csharp
[Fact]
public async Task GetDashboardAsync_ShouldBoundTheQueryWindow()
{
    // The window has to reach back at least to the start of the calendar year, or
    // the YTD series would be missing its earliest months.
    capturedWindowStart!.Value.Should().BeOnOrBefore(yearStart);
    capturedWindowStart.Value.Should().BeAfter(now.AddMonths(-13));
}
```

Plus a 40-invitation test asserting the 25 cap and newest-first ordering.

---

# 12. Swagger exposed in Production

```csharp
// BEFORE
if (app.Environment.IsProduction())
{
    await DatabaseExtensions.IntializeDatabaseAsync(app);
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

Sandbox and UAT deliberately omit Swagger — only Production had it, which suggests the
branch was copied from Development rather than chosen.

```csharp
// AFTER
if (app.Environment.IsProduction())
{
    // Swagger stays off here. It publishes a full map of every endpoint, DTO and
    // parameter - including the anonymous application-form routes - to anyone who
    // can reach the host. Sandbox and UAT already omit it; Production had
    // inherited the Development branch by copy.
    await DatabaseExtensions.IntializeDatabaseAsync(app);
}
```

**Left alone deliberately:** `IntializeDatabaseAsync` still runs at Production startup, so
a migration applies on deploy with no human in the loop. That is an operational decision,
not a code fix — flagging rather than changing it.

---

# 13. Assistant stores grew without bound

```csharp
// BEFORE — AtsChatHistoryStore
private readonly ConcurrentDictionary<Guid, List<AtsChatTurn>> _histories = new();
private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

public SemaphoreSlim GetUserLock(Guid userId) =>
    _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
```

History was capped at 20 messages per user, but a user's *entry* was never removed, and
each added a `SemaphoreSlim` disposed only by an explicit `Clear` call. In a
process-lifetime singleton this grows with users who have **ever** chatted, not users
currently chatting.

```csharp
// AFTER
/// <remarks>
/// Per-user state in a process-lifetime singleton needs an eviction rule, or it grows
/// with the number of distinct users who have ever chatted rather than the number
/// currently chatting. Entries idle for <see cref="SessionLifetime"/> are dropped, and
/// their lock with them.
/// </remarks>
private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(2);

private sealed class UserHistory
{
    public List<AtsChatTurn> Turns { get; } = [];
    public SemaphoreSlim Lock { get; } = new(1, 1);
    public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;
}
```

```csharp
/// <summary>
/// Fetches (or creates) a user's history, stamps it as live, and opportunistically
/// evicts anything idle. Sweeping here rather than on a timer keeps the store free
/// of background machinery; the cost is one pass over a dictionary bounded by the
/// number of users active in the last couple of hours.
/// </summary>
private UserHistory Touch(Guid userId)
{
    RemoveExpired();

    var history = _histories.GetOrAdd(userId, _ => new UserHistory());
    history.LastAccessedUtc = DateTime.UtcNow;
    return history;
}

private void RemoveExpired()
{
    var cutoff = DateTime.UtcNow.Subtract(SessionLifetime);

    foreach (var entry in _histories)
    {
        if (entry.Value.LastAccessedUtc >= cutoff) continue;

        // A user who returns mid-sweep just gets a fresh history - losing an idle
        // conversation is preferable to holding every lock ever created.
        if (_histories.TryRemove(entry.Key, out var removed))
            removed.Lock.Dispose();
    }
}
```

**Why merge the two dictionaries into one:** the lock and the history now share a
lifetime, so evicting one cannot orphan the other. Two parallel dictionaries made that a
manual invariant.

**Why opportunistic sweeping over a timer:** no background service to register, dispose,
or reason about at shutdown. The pass is bounded by recently-active users.

---

# 14. Role ladder duplicated in five services

`AtsAccessScopeResolver` already existed, and its own doc comment listed the copies:

```csharp
// AtsAccessScopeResolver.cs — the existing comment
// The same role ladder ReportService.GetReportsAsync applies inline. Extracted here so
// new features do not add another copy of it. The existing inline copies in
// ReportService, EndorsementSubmissionService, DisputeOrderService, DashboardService and
// AtsAssistantPlugin are intentionally left alone - converting them is a separate,
// behaviour-preserving change.
```

That was a correct call at the time. It became urgent because **findings 3 and 4 were both
services that had to remember to apply the ladder and didn't.** Five copies is five
chances to forget.

Each site collapsed from ~40 lines to 8:

```csharp
// BEFORE — repeated five times
if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId || userId == Guid.Empty)
    return CreateEmptyResult(paginationRequest);

IReadOnlyCollection<int>? clientIds;
Guid? requiredRequestorId;
if (_currentUser.IsPlatformSuperAdmin) { clientIds = null; requiredRequestorId = null; }
else if (_currentUser.AtsRoleId is not { } roleId) return CreateEmptyResult(paginationRequest);
else if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
{
    var assignments = await _userClientRepository.GetUserClientAssignmentsAsync([userId], cancellationToken);
    clientIds = assignments.Select(a => a.ClientId).Distinct().ToArray();
    requiredRequestorId = null;
}
else if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader && _currentUser.AtsClientId is { } clientId)
{
    clientIds = [clientId];
    requiredRequestorId = userId;
}
else return CreateEmptyResult(paginationRequest);
```

```csharp
// AFTER — the same shape in all five
// The role ladder lives in AtsAccessScopeResolver now - this used to be an
// inline copy of it.
if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
    return CreateEmptyResult(paginationRequest);

var clientIds = scope.AuthorizedClientIds;
var requiredRequestorId = scope.RequiredOwnerId;
```

| Service | Dependencies dropped |
|---|---|
| `ReportService` | `ICurrentUser`, `IUserClientRepository` |
| `DashboardService` | `ICurrentUser`, `IUserClientRepository` |
| `DisputeOrderService` | — (both still used elsewhere) |
| `EndorsementSubmissionService` | — (resolver was already injected) |
| `AtsAssistantPlugin` | `IUserClientRepository` |

`AtsAssistantPlugin` kept `ICurrentUser` — it still reads `UserId`/`AtsClientId` for
draft ownership:

```csharp
// AFTER — AtsAssistantPlugin
// Delegates to AtsAccessScopeResolver - this used to be a fourth inline copy of the
// role ladder. The assistant must never see further than the user it answers for.
private async Task<(IReadOnlyCollection<int>? AuthorizedClientIds, Guid? RequiredRequestorId)?>
    ResolveReportScopeAsync(CancellationToken cancellationToken)
{
    if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
        return null;

    return (scope.AuthorizedClientIds, scope.RequiredOwnerId);
}
```

## The test decision worth flagging

Where a test's **subject** was the ladder itself, stubbing the resolver would have deleted
the coverage. Those tests construct a **real** resolver over the existing mocks:

```csharp
// WithdrawnApplicationFilteringTests
// A real resolver over the same mocks. These tests are specifically about
// which clients/requestor a role resolves to, so stubbing the resolver would
// remove the thing under test - and Mock.Of<> returns null, which the
// service reads as "no access".
new AtsAccessScopeResolver(_currentUser.Object, _userClientRepository.Object),
```

That `Mock.Of<>` note is from experience: it caused 6 failures mid-refactor, because a
default mock returns `null` and the services correctly read that as "denied".

Where the test is about what a service **does with** a scope, it sets the scope directly:

```csharp
/// <summary>
/// Sets the access scope the service will see. The role-to-scope ladder itself now
/// lives in AtsAccessScopeResolver and is tested there; these tests only care about
/// which predicates reach the repository.
/// </summary>
private void SetAccessScope(IReadOnlyCollection<int>? authorizedClientIds, Guid? requiredOwnerId)
{
    _accessScopeResolver
        .Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new AtsAccessScope(authorizedClientIds, requiredOwnerId));
}
```

---

# 15. No UI test coverage

`Test/Test/Test.csproj` already referenced the UI project and declared an empty `UI\`
folder — the intent was there, unfilled.

Started with `CsvPreviewParser`: plain C# (no bUnit needed), and the place where a silent
regression is most expensive, since a wrong preview leads to an approved bad import.

```csharp
// ADDED: Test/Test/UI/CsvPreviewParserTests.cs
/// <summary>
/// The bulk-upload preview has to agree with what CsvHelper parses server-side, or the
/// operator approves an import that does not match what they were shown.
/// </summary>
public class CsvPreviewParserTests
{
    [Fact]
    public void Parse_ShouldKeepCommasInsideQuotedFields()
    {
        // The bug this parser replaced: the hand-rolled split turned this into six
        // misaligned columns, so the preview did not match the import.
        var csv = $"{Header}\n\"Dela Cruz, Jr.\",Juan,S,juan@example.com,09171234567";

        var result = CsvPreviewParser.Parse(csv);

        result.Rows.Should().ContainSingle();
        result.Rows[0].Should().HaveCount(5);
        result.Rows[0][0].Should().Be("Dela Cruz, Jr.");
    }

    [Fact]
    public void Parse_ShouldKeepNewlinesInsideQuotedFields()
    {
        var csv = $"{Header}\n\"Line one\nLine two\",Juan,S,juan@example.com,09171234567";

        var result = CsvPreviewParser.Parse(csv);

        // One record, not two - the newline is inside the quotes.
        result.Rows.Should().ContainSingle();
        result.Rows[0][0].Should().Be("Line one\nLine two");
    }
    // ...8 more
}
```

**Not done:** the `ApplicationFormComponent` draft round-trip and the
`CanAddEmployer2`/`CanAddEmployer3` boolean chains need bUnit. Left as follow-up rather
than pulled in half-finished.

---

# Verification

```
dotnet build 1CibiPlatform.sln     →  0 errors, 10 warnings
dotnet test Test/Test/Test.csproj  →  547 passed, 0 failed, 0 skipped (28s)
```

Integration tests ran against real Testcontainers PostgreSQL.

528 → 547 tests. **No test was deleted to make the suite pass.** The 6 that failed
mid-refactor were tests whose subject had moved into `AtsAccessScopeResolver`; each was
repointed at the new seam.

## Follow-ups deliberately not done

| Item | Why |
|---|---|
| Production auto-migration | Operational decision, not a code fix |
| bUnit component tests | Draft round-trip + employer-validation chains |
| Dashboard SQL aggregation | Query is bounded now; urgency removed |
