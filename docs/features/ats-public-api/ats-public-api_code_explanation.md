# ATS Public API — Code Explanation

Companion to [`ats-public-api.md`](ats-public-api.md). That document explains *what* the feature does
and *why* the rules exist. This one exists so a developer can change the implementation without
opening every file cold — it walks the real call chains, names the exact method at each hop, and
quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is
answered by §11.

> **Read §0 first.** The design doc predates several later changes and is now wrong or incomplete in
> ten places, including one claim about error reporting that the code flatly contradicts. Every claim
> below was verified against the code on branch `feature/Update-ReadMe-File`; where the two disagree,
> this document follows the code.

---

## 0. Where the design doc no longer matches the code

| # | `ats-public-api.md` says | The code actually does |
|---|---|---|
| **C1** | Step 1: "`Web/` ← all **27** existing folders, moved verbatim" | **31** folders live under `Features/Web/` today. The move is real; the count is stale |
| **C2** | "Not done" 8: a wrong CSV header row is "**reported through the bulk status endpoint**" | **It is not.** The header check throws `InternalServerException` inside the Quartz job, the generic catch marks the file failed, and the file is released back to `Pending` and retried on every tick forever. `RecordBulkFileRowOutcomeAsync` is only reached on the success path, so the status endpoint reports `Pending` indefinitely with no reason. See §5.3 |
| **C3** | Step 7: withdraw's scope and terminal guards live "in the `UPDATE` predicate **rather than a preceding read**" | There *is* a preceding read. `PublicApiService.WithdrawOrderAsync` calls `_repository.GetOrderAsync` first to distinguish 404 from 409, with an inline comment conceding the read is not what secures the operation (§3.7) |
| **C4** | "Not done" 4: "The base URL in the docs is a placeholder (`https://api.cibi.com.ph`)" | There are **two** base URLs and they contradict each other on the same rendered page. `ApiDocsContent.BaseUrl` is `https://oneplatform.cibi.com.ph/`; all sixteen cURL/C# samples hardcode `https://api.cibi.com.ph` (§8.3) |
| **C5** | "Not done" 2: "`DefaultStrict` is one global bucket … 20/min is shared across every public-API client" | True, and worse than stated: it is shared across all **eight routes** too, because the partition key is the policy-name constant. The sibling `AnonymousApplicationForm` policy in the same `switch` already partitions by client IP, with a comment explaining exactly why (§7.2) |
| **C6** | Nothing about the token endpoint's throttling | `Auth_Login` (`/token/generatetoken` → `/login`) carries **no `Metadata` at all**, so it falls through to the `default` bucket at **500 requests/second**. The browser login beside it carries `LoginPolicy` (5 per 10 s). The credential endpoint that fronts this whole feature is the least throttled route in the platform (§7.3) |
| **C7** | Step 6: "`GET /packages` takes the client from the token … a caller must not be able to read another client's entitlements by passing an id" | Correct as far as it goes, but the guard is `if (clientId is > 0)` in `BuildPackagesQuery`. A token with **no `atsClientId` claim** drops the filter entirely and receives every package on the platform — and `OrderInputValidator` uses the same call, so such a token may also *order* against any package (§9.1) |
| **C8** | Step 2: `OrderHistorySource.PublicApi` "already existed and was **unused**" | Three production call sites today, and the doc names only two. `PublicApiService.WithdrawOrderAsync` is the third (§6) |
| **C9** | Step 10: "`MainLayout`'s `OnInitializedAsync` is **the only** redirect-to-login in the app" | `MainLayout.razor.cs` has two (`NavigateTo("/login")` at lines 70 and 175) and `ATSLayout.razor:214` has a third. The *mechanism* claim holds — `GenericLayout` really does bypass all of them — the superlative does not |
| **C10** | Nothing about a 403 | `PublicApiService.ResolveScopeAsync` throws `ForbiddenException` for a caller with no ATS access, and the docs site's status-code table does not list 403. Worse, the eight routes disagree with each other about what "no ATS access" means: 403, 200-with-an-empty-page, and 404 all occur (§9.2) |

Verified as stated, for the record: **public routes are versionless** — no `v1` segment appears in any
of the eight Carter route literals or the eight gateway `MatchPath` values. And `Features/PublicApi/`
holds exactly eight slices, one folder per operation, each an `*Endpoint.cs` + `*Handler.cs` pair.

One further drift, in a *different* document: `docs/reviews/ats-oneplatform-fix-details.md` §3 quotes
`ResolveRequestedDocuments` with seven candidate document types. The live method has **eleven** —
`NbiClearance`, `Coe1`, `Coe2` and `Coe3` were added afterwards (§9.11).

---

## 1. How a public API caller authenticates

The short answer, and it is the one most likely to surprise: **there is no API key, no client-credentials
grant, and no second authentication scheme.** A public-API caller posts a username and password to the
same endpoint the mobile-style clients use and receives the same JWT the platform mints for anyone else.
The design doc's claim holds. What follows is the whole chain, because "it's just a JWT" hides four
details that matter.

### 1.1 The credential endpoint — `Auth/Features/Login/LoginEndpoint.cs`

```csharp
		app.MapPost("login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
		{

			var command = new LoginCommand(request.username, request.password);

			LoginResult result = await sender.Send(command, cancellationToken);

			LoginResponse loginResponse = new LoginResponse(result.loginResponseDTO);

			return Results.Ok(loginResponse.LoginResponseDTO);
		})
		.WithName("Login")
		.WithTags("Authentication")
		.Produces<LoginResponseDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Login")
		.WithDescription("Login User");
```

Reached from outside as `POST /token/generatetoken` via `Auth/Path/AuthPaths.cs:16-25`:

```csharp
			new RouteDefinitionDTO(
				RouteId: "Auth_Login",
				MatchPath: "/token/generatetoken",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/login" }
				}
			),
```

Note what is absent: **no `Metadata` dictionary**, so no `RateLimitPolicy`. Compare the browser login
immediately below it (`AuthPaths.cs:27-40`), which does carry one. §7.3.

### 1.2 `LoginService.LoginAsync` — the token, and the claims on it

`Auth/Services/Login/LoginService.cs:132-134`:

```csharp
		// produce JWT token
		userData = await AddAtsClaimsAsync(userData);
		string jwtToken = this._jWTService.GetAccessToken(userData);
```

and the private helper at lines 412-419:

```csharp
	private async Task<LoginDTO> AddAtsClaimsAsync(LoginDTO userData)
	{
		var atsClaims = await _atsAccessClaimsProvider.GetClaimsAsync(userData.Id);
		return userData with
		{
			AtsClientId = atsClaims?.AtsClientId,
			AtsRoleId = atsClaims?.AtsRoleId
		};
	}
```

`IAtsAccessClaimsProvider` is the sanctioned cross-module hop — the interface lives in
`Auth/Shared/Contracts/`, the implementation in `ATS/Shared/Implementations/AtsAccessClaimsProvider.cs`,
registered at `ATSServiceConfiguration.cs:163`. Auth therefore never queries an ATS table directly.

The implementation is defensive in a way that matters here, because **it returns `null` in four
different situations**, each of which produces a token with no ATS claims at all:

```csharp
		if (accessRows.Count == 0)
			return null;

		var roleIds = accessRows.Select(row => row.RoleId).Distinct().ToArray();
		if (roleIds.Length != 1)
		{
			_logger.LogWarning(
				"ATS claims were omitted because user {UserId} has inconsistent roles",
				userId);
			return null;
		}
```

…then separately nulls out just the client id when the assigned client is inactive, or when
`UserClientDetails` and `UserDetails` disagree about which client the user belongs to. A user with two
ATS module rows carrying different `RoleId`s — an ordinary data-entry accident — silently becomes a
principal with no ATS identity. §9.1 explains why that is not a safe failure.

`LoginAsync` also does two things the browser path (`LoginWebAsync`, line 197) does not:

```csharp
		_httpContextAccessor.HttpContext!.Response.Cookies.Append(_httpCookieOnlyKey!, jwtToken, cookieOptions);
```

The same token is returned **in the response body** *and* set as an HttpOnly cookie. A machine caller
uses the body; the cookie is a side effect they did not ask for and, per §1.4, one that can come back
to bite them.

And it calls `_jWTService.GetAccessToken(userData)` with **no `sessionId`**, where `LoginWebAsync` calls
`GetAccessToken(userData, session.Id)`. That single omitted argument is why the public-API token is not
revocable — §1.5.

### 1.3 Claim emission — `Auth/Services/Login/JWTService.cs:75-79`

Inside `private IEnumerable<Claim> GetClaims(LoginDTO loginDTO, int? sessionId)`:

```csharp
		if (loginDTO.AtsRoleId is > 0)
			claims.Add(new Claim(AuthClaimTypes.AtsRoleId, loginDTO.AtsRoleId.Value.ToString(CultureInfo.InvariantCulture)));

		if (loginDTO.AtsClientId is > 0)
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, loginDTO.AtsClientId.Value.ToString(CultureInfo.InvariantCulture)));
```

**Both are conditional.** A user with no ATS access gets a perfectly valid token that simply lacks
`atsRoleId` and `atsClientId`. Claim names are `"atsClientId"` / `"atsRoleId"`
(`Auth/Constants/AuthClaimTypes.cs:12-13`).

The `Sid` claim is likewise conditional (`if (sessionId is > 0)`), so a `LoginAsync` token has none.

Lifetime comes from configuration, `JWTService.cs:15-31`:

```csharp
		var jwtSettings = _configuration.GetSection("Jwt");
		var key = jwtSettings["Key"];
		var issuer = jwtSettings["Issuer"];
		var audience = jwtSettings["Audience"];
		var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"]!);
```

### 1.4 Reading the claims back — `Auth/Shared/Implementations/CurrentUser.cs`

```csharp
	public int? AtsClientId => ParsePositiveInt(GetClaimValue(AuthClaimTypes.AtsClientId));

	public int? AtsRoleId => ParsePositiveInt(GetClaimValue(AuthClaimTypes.AtsRoleId));
```

with

```csharp
	private static int? ParsePositiveInt(string? value) =>
		int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
			? parsed
			: null;
```

So an absent claim and a present-but-zero claim are indistinguishable downstream: both are `null`. Every
consumer in this feature reads `ICurrentUser.AtsClientId` and must be correct for `null`.

**The cookie overrides the bearer header.** `API/APIs/ServiceConfig/ServiceConfiguration.cs:197-204`:

```csharp
				OnMessageReceived = context =>
				{
					if (context.Request.Cookies.TryGetValue(_httpCookieOnlyKey!, out var token))
					{
						context.Token = token;
					}
					return Task.CompletedTask;
				},
```

`JwtBearerHandler` populates `context.Token` from `Authorization: Bearer …` *before* this event fires,
and this assignment is unconditional. A request carrying both credentials is authenticated as **the
cookie's user**, and the bearer header is silently discarded — no error, no log. §9.4.

### 1.5 Validation, and why the public token cannot be revoked

`ServiceConfiguration.cs:181-193`:

```csharp
		.AddJwtBearer(options =>
		{
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				ValidIssuer = issuer,
				ValidAudience = audience,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
				RoleClaimType = ClaimTypes.Role,
				ClockSkew = TimeSpan.Zero
			};
```

`ClockSkew = TimeSpan.Zero` is unusually strict and worth preserving — it means an expired integration
token stops working the instant it expires, with no five-minute grace.

Then `OnTokenValidated` (`ServiceConfiguration.cs:205-222`):

```csharp
					var sessionClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
					if (string.IsNullOrWhiteSpace(sessionClaim))
						return; // API/SSO tokens without a browser refresh session keep their existing behavior.
```

A `LoginAsync` token has no `Sid` (§1.3), so this returns immediately and `IAuthSessionValidator` is
never consulted. The browser path *is* session-checked and fails with
`"Authentication session is no longer active."` when the refresh session is gone.

**Consequence:** there is no revocation path for a public-API token. Logging the user out, deleting
their session, or deactivating their ATS access does not invalidate a token already issued; it stays
good for the full `Jwt:ExpiryInMinutes`. The only levers are the signing key and the clock.

### 1.6 The authorization gate is "authenticated", nothing more

`ServiceConfiguration.cs:263` is the entire authorization configuration:

```csharp
		services.AddAuthorization();
```

No policies, no `FallbackPolicy`, no default requirement beyond an authenticated identity. Every one of
the eight public routes ends with a bare `.RequireAuthorization()`, which therefore means **"any valid
token for any user of this platform"** — including a SAML SSO principal and including a user with no
ATS access whatsoever. Everything ATS-specific is enforced *inside* the handlers, by
`IAtsAccessScopeResolver` or not at all. §9.1 and §9.2 map which routes actually do.

---

## 2. One request end to end — `GET /publicapi/ats/orders/{orderId}`

`GetOrder` is traced in full because it is the only slice that goes through `IPublicApiService`, and
`PublicApiService` is where this feature's two central rules — scope every read, and 404 not 403 — are
actually implemented. The other seven are diffed against it in §3.

```
GET /publicapi/ats/orders/{orderId}
  → YARP route "PublicGetOrder" (Path/ATSPaths.cs:357-370)
      PathPattern → /api/public/ats/orders/{orderId};  RateLimitPolicy = DefaultStrict
  → JWT bearer authentication (ServiceConfiguration.cs:181)
  → .RequireAuthorization()  — "is authenticated", nothing more
  → GetOrderEndpoint.AddRoutes                       (Carter)
    → sender.Send(GetOrderQueryRequest)              [MediatR]
      → LoggingBehavior → ValidationBehavior
        → GetOrderQueryRequestValidator              [FluentValidation]
      → GetOrderHandler.Handle
        → IPublicApiService.GetOrderAsync
          → IAtsAccessScopeResolver.ResolveAsync     → AtsAccessScope | 403
          → IPublicApiRepository.GetOrderAsync        → scoped SQL, or null
          → OrderStatusHistories (second query)
        ← null → NotFoundException (404)
    ← Results.Ok(GetOrderEndpointResponse)            [200]
```

### 2.1 The gateway route — `Path/ATSPaths.cs:357-370`

```csharp
			new RouteDefinitionDTO(
				RouteId: "PublicGetOrder",
				MatchPath: "/publicapi/ats/orders/{orderId}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathPattern", "/api/public/ats/orders/{orderId}" }
				},
				Metadata: new Dictionary<string, string>
				{
					{ "RateLimitPolicy", GatewayConstants.RateLimitPolicies.DefaultStrict }
				}
			),
```

The block is introduced by a comment that states the intent of the whole surface
(`ATSPaths.cs:275-280`):

```csharp
			// ---- Public API ----------------------------------------------------
			// Client integrations, authenticated with a token from
			// /token/generatetoken. DefaultStrict (20/min) rather than the 500/s
			// default: these are machine callers, and an integration in a retry loop
			// must not be able to saturate the platform.
```

Two mapping rules, and getting them backwards is the classic bug here:

- **`PathSet`** for a literal path — used by the five routes with no template segment.
- **`PathPattern`** for anything with `{…}`. The code says so explicitly at `ATSPaths.cs:311`:
  `// PathPattern, not PathSet: PathSet would forward the literal "{fileId}".`

So the public prefix `/publicapi/ats/…` maps to the backend prefix `/api/public/ats/…`. **The two
prefixes are worded differently on purpose** — the public one reads as a namespace, the backend one as
a path — and nothing enforces the pairing at compile time. The `RouteId` string is a third copy of the
Carter route's `.WithName(...)` value, also unenforced. §11.

### 2.2 Endpoint — `Features/PublicApi/GetOrder/GetOrderEndpoint.cs`

```csharp
public record GetOrderEndpointResponse(PublicOrderDetailDTO Order);

public class GetOrderEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("api/public/ats/orders/{orderId:guid}", async (
			Guid orderId,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetOrderQueryRequest(orderId);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetOrderEndpointResponse(result.Order));
		})
		.WithName("PublicGetOrder")
		.WithTags("ATS Public API")
		.Produces<GetOrderEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get an order")
		.WithDescription(
			"Returns one order's current status, its OMS ticket number once raised, and "
			+ "its event history. Returns 404 when the order does not belong to the "
			+ "access token's client.")
		.RequireAuthorization();
	}
}
```

The `:guid` route constraint is load-bearing and easy to lose: without it, `orders/withdraw` and
`orders/{orderId}` become ambiguous against each other. Every public route that takes an id uses it.

There is **no per-slice DI registration anywhere.** `AddATSCarterModules` in
`ServiceConfig/ATSServiceConfiguration.cs` scans the assembly for every `ICarterModule`:

```csharp
	public static IServiceCollection AddATSCarterModules(this IServiceCollection services, Assembly assembly)
	{
		services.AddCarter(configurator: c =>
		{
			var modules = assembly.GetTypes()
				.Where(t => typeof(ICarterModule).IsAssignableFrom(t) && !t.IsAbstract)
				.ToArray();
			c.WithModules(modules);
		});
		return services;
	}
```

Adding a ninth slice means adding a folder and a gateway route. Nothing else.

### 2.3 Query, validator, handler — `GetOrderHandler.cs`

The whole file:

```csharp
public record GetOrderQueryRequest(Guid OrderId) : IQuery<GetOrderQueryResult>;

public record GetOrderQueryResult(PublicOrderDetailDTO Order);

public class GetOrderQueryRequestValidator : AbstractValidator<GetOrderQueryRequest>
{
	public GetOrderQueryRequestValidator()
	{
		RuleFor(x => x.OrderId)
			.NotEmpty().WithMessage("Order ID is required.");
	}
}

public class GetOrderHandler : IQueryHandler<GetOrderQueryRequest, GetOrderQueryResult>
{
	private readonly IPublicApiService _publicApiService;

	public GetOrderHandler(IPublicApiService publicApiService)
	{
		_publicApiService = publicApiService;
	}

	public async Task<GetOrderQueryResult> Handle(
		GetOrderQueryRequest request,
		CancellationToken cancellationToken)
	{
		var order = await _publicApiService.GetOrderAsync(request.OrderId, cancellationToken);

		return new GetOrderQueryResult(order);
	}
}
```

Validators run automatically through `config.AddOpenBehavior(typeof(ValidationBehavior<,>))`
(`ATSServiceConfiguration.cs:28`), registered once for the module rather than per feature. Note how
little this validator has to do: the id is a `Guid` bound by the route constraint, so there is nothing
left to check but emptiness. **No client id appears in the request at any point** — that is the design,
not an oversight.

### 2.4 The service — `Services/PublicApi/PublicApiService.cs:22-33`

```csharp
	public async Task<PublicOrderDetailDTO> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
	{
		var accessScope = await ResolveScopeAsync(cancellationToken);

		var order = await _repository.GetOrderAsync(
			orderId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		return order ?? throw NotFound(orderId);
	}
```

Three lines doing three jobs: resolve *who is asking*, push that identity *into the query* rather than
filtering afterwards, and translate "no row" into a 404. The two private helpers at the bottom of the
file are where the feature's rules are written down:

```csharp
	private async Task<AtsAccessScope> ResolveScopeAsync(CancellationToken cancellationToken) =>
		await _scopeResolver.ResolveAsync(cancellationToken)
			?? throw new ForbiddenException("The access token does not grant ATS access.");

	// Out of scope reads as not found, never forbidden: a 403 would confirm that an
	// order belonging to another client exists.
	private static NotFoundException NotFound(Guid orderId) =>
		new($"Order with ID {orderId} not found.");
```

**These two are deliberately different status codes and it is worth being precise about why.** A caller
with *no ATS access at all* gets 403 — there is no record whose existence is being confirmed, only a
statement about their token. A caller *with* ATS access who asks for somebody else's order gets 404,
because a 403 there would answer the question "does order X exist?" with "yes, and it isn't yours".
The guide's rule ("out-of-scope reads return 404, never 403") is about the second case, and the code
honours it exactly.

### 2.5 The scope ladder — `Services/AccessScope/AtsAccessScopeResolver.cs`

The contract first, because the nullability is the whole design
(`Services/AccessScope/IAtsAccessScopeResolver.cs`):

```csharp
/// <param name="AuthorizedClientIds">
/// null means every client (platform super admin). An empty collection means no client,
/// which filters everything out - empty is not the same as null.
/// </param>
/// <param name="RequiredOwnerId">
/// When set, the caller may only see records they personally created.
/// </param>
public readonly record struct AtsAccessScope(
	IReadOnlyCollection<int>? AuthorizedClientIds,
	Guid? RequiredOwnerId);
```

The ladder:

```csharp
	public async Task<AtsAccessScope?> ResolveAsync(CancellationToken cancellationToken)
	{
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return null;
		}

		if (_currentUser.IsPlatformSuperAdmin)
		{
			return new AtsAccessScope(null, null);
		}

		if (_currentUser.AtsRoleId is not { } roleId)
		{
			return null;
		}

		if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);

			var clientIds = assignments
				.Select(assignment => assignment.ClientId)
				.Distinct()
				.ToArray();

			return new AtsAccessScope(clientIds, null);
		}

		if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			&& _currentUser.AtsClientId is { } clientId)
		{
			return new AtsAccessScope([clientId], userId);
		}

		return null;
	}
```

Four outcomes, and each is a different shape of filter:

| Caller | `AuthorizedClientIds` | `RequiredOwnerId` | Effect |
|---|---|---|---|
| Unauthenticated / no user id | — | — | `null` → 403 |
| Platform super admin | `null` | `null` | sees every client's orders |
| `PlatformManager` / `Admin` | their assigned client ids | `null` | sees those clients, any requestor |
| `User` / `Uploader` | `[AtsClientId]` | their own `UserId` | sees **only orders they personally raised** |
| Anything else (incl. no `atsRoleId`) | — | — | `null` → 403 |

The last row of the `User`/`Uploader` case is the one integrators hit without realising it: a token
whose ATS role is `User` cannot read an order a colleague created, even inside the same client. That is
correct for the console and surprising over an API, where the "user" is often a shared service account.
It is also why the `RequiredOwnerId` predicate compares `RequestorId`, not `ClientId`.

The class header records why this exists at all:

```csharp
// The same role ladder ReportService.GetReportsAsync applies inline. Extracted here so
// new features do not add another copy of it. The existing inline copies in
// ReportService, EndorsementSubmissionService, DisputeOrderService, DashboardService and
// AtsAssistantPlugin are intentionally left alone - converting them is a separate,
// behaviour-preserving change.
```

That comment is now out of date in one respect: `ReportService` **has** been converted — it injects
`IAtsAccessScopeResolver` (`ReportService.cs:11`) and calls it in six methods. The public API's
`GetOrders` and `DownloadReport` slices depend on that conversion; see §3.

### 2.6 The repository — `Data/Repository/PublicApi/PublicApiRepository.cs`

The class comment explains a registration decision that looks like an oversight:

```csharp
// Not cached and not decorated: an integrating client polls these to watch an order
// move, so a cached page would report the staleness they are polling to avoid. Same
// reasoning as BulkUploadRepository and OMSTicketingRepository.
public sealed class PublicApiRepository : IPublicApiRepository
```

The scope predicate is a single private helper used by both the read and the write:

```csharp
	// A null client set means unrestricted (super admin); an empty set filters
	// everything out. Mirrors the rule every other ATS read applies.
	private static IQueryable<EmailInvitationRequest> ApplyOrderScope(
		IQueryable<EmailInvitationRequest> query,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId) =>
		query.Where(invitation => (authorizedClientIds == null
				|| (invitation.ClientId.HasValue && authorizedClientIds.Contains(invitation.ClientId.Value)))
			&& (!requiredRequestorId.HasValue
				|| invitation.RequestorId == requiredRequestorId.Value));
```

`invitation.ClientId.HasValue &&` is not decoration. `ClientId` is nullable, and without that check a
`Contains` over a null would either throw in translation or, worse, be coerced. An order with
`ClientId = null` is therefore invisible to every scoped caller and visible only to a super admin —
which is exactly how §9.1's orphaned orders disappear.

The read projects into the public DTO and never returns an entity:

```csharp
		var order = await ApplyOrderScope(
				_dbContext.EmailInvitationRequests.AsNoTracking(),
				authorizedClientIds,
				requiredRequestorId)
			.Where(invitation => invitation.EmailInvitationID == orderId)
			.Select(invitation => new PublicOrderDetailDTO
			{
				OrderId = invitation.EmailInvitationID,
				FirstName = invitation.FirstName,
				MiddleInitial = invitation.MiddleInitial,
				LastName = invitation.LastName,
				EmailAddress = invitation.EmailAddress,
				MobileNumber = invitation.MobileNumber,
				Package = invitation.SelectPackage,
				OrderType = invitation.RushNormal,
				OrderStatus = invitation.OrderStatus,
				ApplicationFormStatus = invitation.ApplicationFormStatus,
				TicketNumber = invitation.TicketNumber,
				TicketDeliveryDate = invitation.TicketDeliveryDate,
				OrderCreatedAt = invitation.OrderCreatedAt,
				FormCompletedAt = invitation.FormCompletedAt,
				OrderCompletedAt = invitation.OrderCompletedAt
			})
			.FirstOrDefaultAsync(cancellationToken);
```

Note the renaming at the boundary: `EmailInvitationID` → `OrderId`, `SelectPackage` → `Package`,
`RushNormal` → `OrderType`. Those are the internal console names, and this projection is the one place
they are translated. `GetOrders` does **not** do this — §3.1.

The timeline is a deliberately separate round trip:

```csharp
		// Fetched separately rather than as a correlated subquery: the timeline is a
		// second, ordered result set and this keeps the projection above flat.
		order.History = await _dbContext.OrderStatusHistories
			.AsNoTracking()
			.Where(history => history.EmailInvitationRequestId == orderId)
			.OrderBy(history => history.OccurredAt)
```

**This second query is not scope-filtered**, and does not need to be: it is only reached when the first
query returned a row, and the first query was scoped. It filters on `orderId` alone. Reordering these
two — or short-circuiting the null check between them — would turn it into an unscoped read of another
client's audit trail. The `if (order is null) return null;` between them is a security control, not a
nicety.

`Source` is projected into each history row, which is what makes §6 visible to a caller.

---

## 3. The other seven slices, as diffs from §2

| Slice | Route | Backend path | Reuses | Scope enforced by | No-ATS-access |
|---|---|---|---|---|---|
| `CreateEndorsement` | `POST` | `/api/public/ats/endorsements` | `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` | `OrderInputValidator` + `_currentUser.AtsClientId` on write | **nothing** (§9.1) |
| `CreateBulkEndorsement` | `POST` | `/api/public/ats/endorsements/bulk` | `EndorsementSubmissionService.InsertBulkSubjectAsync` | same | **nothing** (§9.1) |
| `GetOrders` | `GET` | `/api/public/ats/orders` | `ReportService.GetReportsAsync` | `IAtsAccessScopeResolver`, inside the service | 200, empty page |
| `GetPackages` | `GET` | `/api/public/ats/packages` | `PackageManagementService.GetPackagesAsync(…, clientId)` | `_currentUser.AtsClientId` only | 200, **every package** (§9.1) |
| `GetBulkUploadStatus` | `GET` | `/api/public/ats/endorsements/bulk/{fileId}` | `PublicApiService` (new read) | inlined scope predicate in the repository | 403 |
| `DownloadReport` | `POST` | `/api/public/ats/orders/{orderId}/report` | `ReportService.DownloadIndividualReportAsync` | `IAtsAccessScopeResolver`, inside the service | 404 |
| `WithdrawOrder` | `PATCH` | `/api/public/ats/orders/{orderId}/withdraw` | `PublicApiService` (new write) | `ApplyOrderScope` in the `UPDATE` predicate | 403 |

All eight carry `.RequireAuthorization()` and all eight gateway routes carry `DefaultStrict`. Both
checks pass with no exceptions — see §7.1.

### 3.1 `GetOrders` — same shape, but it returns the *internal* DTO

`GetOrdersHandler.Handle` in full:

```csharp
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		// GetReportsAsync resolves the caller's scope itself, so a token scoped to one
		// client can only ever see that client's orders.
		var orders = await _reportService.GetReportsAsync(paginationRequest, cancellationToken);

		return new GetOrdersQueryResult(orders);
```

The response type is `KeysetPaginatedResult<ReportListDTO>` — **the console's own list DTO**, not a
public one. `DTO/ReportListDTO.cs`:

```csharp
public record ReportListDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? SubjectName { get; set; }
	// The name parts SubjectName is built from, so the edit dialog can prefill
	// each field without refetching the order.
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? LastName { get; set; }
	public string? Requestor { get; set; }
	public string? TicketNumber { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
	public string? SelectedPackage { get; set; }
	public string? RushNormal { get; set; }
	public string? HitStatus { get; set; }
}
```

Two consequences, both worth a decision rather than an accident:

- **The list and the detail disagree on field names for the same order.** `GET /orders` yields
  `emailInvitationRequestId`, `selectedPackage`, `rushNormal`; `GET /orders/{orderId}` yields `orderId`,
  `package`, `orderType`. An integrator must know these are the same values. The docs site shows both
  samples but never says so (§8.3).
- **`Requestor` is the internal staff member's display name** and `HitStatus` is an internal screening
  outcome. Both are serialised to external callers. `PublicOrderDetailDTO` exposes neither. §9.3.

The scope itself is enforced inside the shared service, `ReportService.cs:199-205`:

```csharp
		// The role ladder lives in AtsAccessScopeResolver now - this used to be an
		// inline copy of it.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			return new KeysetPaginatedResult<ReportListDTO>(Array.Empty<ReportListDTO>(), null, 0);
		}
```

That is the **200-with-an-empty-page** behaviour in the table above, and it is the console's choice, not
the public API's: for a browser list, an empty table is a better answer than an error dialog. Reusing
the service inherited it. `PublicApiService` would have thrown 403 for the same caller.

Pagination is keyset, not offset — `KeysetPage.Clamp` bounds the page size and the cursor is
`CursorCodec.Encode(page[^1].OrderCreatedAt?.ToString("O"), …)`. The endpoint validator caps `PageSize`
at 100 independently:

```csharp
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
```

Two ceilings on the same value in two layers. `KeysetPage.Clamp` wins if they ever disagree.

### 3.2 `GetPackages` — the client id is passed *as a parameter*, not resolved

`GetPackagesHandler` is the one slice that injects `ICurrentUser` directly:

```csharp
		// The client comes from the token, never from the request: a caller must not be
		// able to read another client's entitlements by passing an id.
		var packages = await _packageManagementService.GetPackagesAsync(
			paginationRequest,
			cancellationToken,
			_currentUser.AtsClientId);
```

The comment is right about the request and silent about the null. `IPackageManagementService` declares
the parameter as `int? clientId = null`, and `PackageManagementService.GetPackagesAsync` forwards it
unchanged to `ATSRepository.GetPackagesPageAsync`, where the filter is built
(`Data/Repository/PackageManagement/ATSRepository.Packages.cs:29-38`):

```csharp
	private IQueryable<PackageDetails> BuildPackagesQuery(string? searchTerm, int? clientId)
	{
		var query = _dbcontext.PackageDetails.AsNoTracking();
		if (clientId is > 0)
			query = query.Where(package => _dbcontext.ClientDetails.Any(client =>
				client.ClientId == clientId.Value && client.PackageId == package.PackageId));
		if (!string.IsNullOrEmpty(searchTerm))
			query = query.Where(package =>
				EF.Functions.ILike(package.PackageName, $"%{searchTerm}%") ||
				EF.Functions.ILike(package.PackageDescription, $"%{searchTerm}%"));
		return query;
	}
```

`if (clientId is > 0)` — **`null` means no filter.** That is the right semantics for the console's
Package Management screen, where an administrator is meant to see every package. It is the wrong
semantics for a public endpoint, and it is the root of §9.1.

Also note what the projection returns, since it goes straight over the wire:

```csharp
			.Select(package => new PackageDetailsDTO
			{
				PackageId = package.PackageId,
				PackageName = package.PackageName,
				PackageDescription = package.PackageDescription,
				IsActive = package.IsActive,
				FollowUpEmail = package.FollowUpEmail,
				CreatedAt = package.CreatedAt,
				UpdatedAt = package.UpdatedAt
			}).ToListAsync(cancellationToken);
```

`PackageDescription` is the free-text field `OMSTicketPayloadMapper.TryParseReportTypeId` parses the
legacy OMS report-type code out of. §9.3.

### 3.3 `CreateEndorsement` — a thin layer, and a duplicated validator

The handler is genuinely thin:

```csharp
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = request.FirstName,
			LastName = request.LastName,
			MiddleInitial = request.MiddleInitial,
			EmailAddress = request.EmailAddress,
			MobileNumber = request.MobileNumber,
			SelectPackage = request.Package,
			RushNormal = request.OrderType
		};

		// Identical to the web path apart from the source, which is what makes an order
		// traceable to the integration that raised it. The service validates the
		// package and order type against this client and throws BadRequestException
		// when either is not theirs.
		var isSuccessful = await _endorsementSubmissionService.InsertEmailInvitationRequestAsync(
			dto,
			cancellationToken,
			OrderHistorySource.PublicApi);
```

The web console's equivalent is one line, `Features/Web/InsertEmailInvitationRequest/InsertEmailInvitationRequestHandler.cs:51`:

```csharp
		var isAdded = await _endorsementSubmissionService.InsertEmailInvitationRequestAsync(request.emailInvitationRequestDTO, cancellationToken);
```

Same method, same DTO, one argument different. **"Reuse the service, do not fork it" holds here** — and
it holds because the missing piece (`source`) was added to the service as an optional parameter rather
than copied into a second service.

The id echo works by writing back onto the DTO the caller already owns, which is why the return type
did not have to change:

```csharp
		// Echoed back in their canonical spelling, so a caller who sent "rush" can see
		// it was stored as "Rush".
		return new CreateEndorsementResult(
			isSuccessful,
			dto.OrderId,
			dto.SelectPackage ?? string.Empty,
			dto.RushNormal ?? string.Empty);
```

`dto.OrderId`, `dto.SelectPackage` and `dto.RushNormal` were all mutated *by the service*
(`EndorsementSubmissionService.cs:110-113` and `:135`). **This is the one place in the feature where a
method's contract is carried by mutation rather than by its return value** — reading the handler alone
gives no hint that `dto` comes back populated. §11.

**Where logic WAS duplicated.** `CreateEndorsementCommandValidator` is a near-verbatim copy of
`EmailInvitationRequestCommandValidator`, and says so:

```csharp
	// Mirrors EmailInvitationRequestCommandValidator: the public API must not accept
	// anything the web console would reject, or the two paths would drift.
```

They have already drifted, in both directions:

| Rule | Web (`EmailInvitationRequestCommandValidator`) | Public (`CreateEndorsementCommandValidator`) |
|---|---|---|
| `FirstName` / `LastName` | required, ≤ 50 | required, ≤ 50 — identical |
| `MiddleInitial` | **no rule at all** | `MaximumLength(255)` |
| `EmailAddress` | `.NotEmpty().EmailAddress().WithMessage("Email is required.")` | `.NotEmpty().WithMessage(…).EmailAddress().WithMessage(…)` — two distinct messages |
| `MobileNumber` | `^\d{11}$` | `^\d{11}$` — identical |
| `Package` | required, ≤ 100 | required, ≤ 100 — identical |
| `OrderType` | `OrderType.Normalize(…) is not null` | identical |

Nothing enforces the "mirrors" claim. The `MiddleInitial` asymmetry is harmless (255 matches the EF
column, `EmailInvitationRequestConfiguration.cs:23-24`). The mobile rule is the one integrators will
hit: see §3.4.

### 3.4 `CreateBulkEndorsement` — 202, `IFormFile`, and a different mobile rule

```csharp
			// 202: the file is queued here and parsed by a background job within
			// seconds. Per-row results are read back from the status endpoint below.
			return Results.Accepted(
				$"/api/public/ats/endorsements/bulk/{result.FileId}",
				new CreateBulkEndorsementEndpointResponse(result.FileId, result.Accepted));
```

`Results.Accepted` sets `Location`. **The `Location` value is the *backend* path, not the public one** —
an integrator following it literally hits `/api/public/ats/…`, which the gateway does not expose. It
needs rewriting to `/publicapi/ats/…` by hand. §9.5.

`.DisableAntiforgery()` is required here and only here, because this is the one `[FromForm]` endpoint;
ASP.NET Core 8+ validates antiforgery for form-bound minimal APIs by default, and a machine caller has
no antiforgery token.

The handler:

```csharp
		// Same upload path as the console: the file is stored, a Pending row is written,
		// and the Quartz job parses it. Only the source differs.
		var accepted = await _endorsementSubmissionService.InsertBulkSubjectAsync(
			dto,
			cancellationToken,
			OrderHistorySource.PublicApi);

		// Set by the service once the file row exists, so the caller can poll it.
		return new CreateBulkEndorsementResult(dto.FileId, accepted);
```

Same DTO-write-back trick as §3.3.

The validator enforces the size and extension bounds:

```csharp
	// 10 MB. The CSV holds five short columns per subject, so this is far more than a
	// realistic batch needs while still refusing an accidental upload of the wrong file.
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;
```

```csharp
			RuleFor(x => x.File.FileName)
				.Must(fileName => fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
				.WithMessage("The file must be a .csv.");
```

**The mobile-number rule differs between the two public write endpoints.** `POST /endorsements`
requires `^\d{11}$`, so `+639171234567` is a 400. The CSV path runs each row through
`BulkSubjectRowValidator.NormalizeMobileNumber`, which accepts it:

```csharp
		var digits = new string(value.Where(char.IsDigit).ToArray());

		// +63 917... and 63917... both denote the same local 0917... number.
		if (digits.Length == 12 && digits.StartsWith("63", StringComparison.Ordinal))
		{
			digits = string.Concat("0", digits.AsSpan(2));
		}

		// A bare 9-prefixed mobile number is missing only its trunk zero.
		if (digits.Length == 10 && digits.StartsWith('9'))
		{
			digits = "0" + digits;
		}

		return digits.Length == MobileNumberLength
			? digits
			: null;
```

The docs site documents both behaviours correctly and separately, so this is a deliberate asymmetry
rather than a bug — but it is the same field on the same public surface with two different acceptance
rules, and an integrator who built their CSV pipeline around `+63…` will get a 400 the moment they
switch to the single endpoint.

### 3.5 `GetBulkUploadStatus` — a pure primary-key read

Same shape as §2: endpoint → validator (`NotEmpty` on `FileId`) → handler → `IPublicApiService`. The
service method is a four-line clone of `GetOrderAsync`:

```csharp
	public async Task<PublicBulkUploadStatusDTO> GetBulkUploadStatusAsync(Guid fileId, CancellationToken cancellationToken)
	{
		var accessScope = await ResolveScopeAsync(cancellationToken);

		var status = await _repository.GetBulkUploadStatusAsync(
			fileId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		return status ?? throw new NotFoundException($"Bulk upload with ID {fileId} not found.");
	}
```

Two things differ in the repository. First, the scope predicate is **inlined rather than shared** —
`ApplyOrderScope` is typed to `IQueryable<EmailInvitationRequest>` and cannot serve
`BulkUploadFileDetails`:

```csharp
			.Where(upload => (authorizedClientIds == null
					|| (upload.ClientId.HasValue && authorizedClientIds.Contains(upload.ClientId.Value)))
				&& (!requiredUploaderId.HasValue
					|| upload.UploadedByUserId == requiredUploaderId.Value))
```

Same shape, different owner column: `UploadedByUserId` plays the role `RequestorId` plays on an order.
A `User`-role token therefore sees only bulk files *it* uploaded, not its client's. Second, the
parameter is renamed at the boundary — the service passes `accessScope.RequiredOwnerId` into a parameter
called `requiredUploaderId`. Correct, but the rename is invisible from either file alone.

The rejected rows are deserialised, not queried:

```csharp
		// Stored as JSON because the rejected rows never became entities - they were
		// refused before insert, so there is no table to read them back from.
		var rejectedRows = string.IsNullOrWhiteSpace(file.RejectedRows)
			? []
			: JsonSerializer.Deserialize<List<BulkUploadRejectedRowDTO>>(file.RejectedRows) ?? [];
```

This is the read half of the design doc's Step 5, and the comment is the answer to "why store a count
you could compute": you cannot, because the rows were never inserted.

### 3.6 `DownloadReport` — see §6, it has its own section

### 3.7 `WithdrawOrder` — the only slice that is not a wrapper

This is the one new write, and the design doc's Step 7 is right that it needed building. The service
(`PublicApiService.cs:48-102`) is longer than the other two methods combined:

```csharp
		var accessScope = await ResolveScopeAsync(cancellationToken);

		// Read first only to distinguish "not yours" from "already terminal": the write
		// below re-applies the scope, so this read is not what secures the operation.
		var order = await _repository.GetOrderAsync(
			orderId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		if (order is null)
		{
			_logger.LogWarning("Withdraw denied for an unknown or out-of-scope order: {@Context}", logContext);

			throw NotFound(orderId);
		}

		var previousStatus = order.OrderStatus;
```

That comment is the correction to **C3**. The read exists, and it exists for a good reason: without it
there is no way to tell a 404 from a 409, and both would have to collapse into one status. What makes
it safe is that the read is *advisory* — the write re-applies the same predicate atomically.

```csharp
		var withdrawn = await _repository.WithdrawOrderAsync(
			orderId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		if (!withdrawn)
		{
			_logger.LogWarning("Withdraw rejected, the order is already terminal: {@Context}", logContext);

			throw new ConflictException(
				"This order can no longer be withdrawn. It is already withdrawn or completed.");
		}
```

The repository is where the correctness lives:

```csharp
		// Scope and terminal-state guards live in the UPDATE predicate, so a concurrent
		// completion or a second call updates nothing rather than racing a read.
		var updated = await ApplyOrderScope(
				_dbContext.EmailInvitationRequests,
				authorizedClientIds,
				requiredRequestorId)
			.Where(invitation => invitation.EmailInvitationID == orderId
				&& invitation.ApplicationFormStatus != ApplicationFormStatus.Withdrawn
				&& invitation.OrderStatus != OrderStatus.Completed)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.ApplicationFormStatus, x => ApplicationFormStatus.Withdrawn)
				.SetProperty(x => x.OrderStatus, x => OrderStatus.ApplicationWithdrawn)

				// The search projection is denormalized, so it has to be rebuilt with
				// the new status.
				.SetProperty(x => x.NeedsProjection, x => true),
				cancellationToken);

		return updated > 0;
```

Four properties of this statement carry the guarantee:

- **`ExecuteUpdateAsync` is a single `UPDATE … WHERE`** — no read-modify-write, so two simultaneous
  withdraws cannot both see the old status. One gets `updated == 1`, the other `updated == 0` and
  therefore a 409.
- **`.AsNoTracking()` is absent here**, unlike every read in the file. That is required:
  `ExecuteUpdateAsync` operates on the queryable directly and a tracking query would be a mistake.
- **`NeedsProjection = true`** is the coupling to the applicant-search projection. Forget it and the
  withdrawn order keeps appearing in search with its old status. It is invisible from this file — the
  consumer is `IApplicantSearchProjectionService`. §11.
- **`OrderStatus.ApplicationWithdrawn` is written without checking the current value** beyond
  "not Completed". An order in any non-terminal state moves to `ApplicationWithdrawn`, and
  `previousStatus` (captured in the read above) is what the audit entry records as the "from".

Then the audit entry — the third `OrderHistorySource.PublicApi` call site:

```csharp
		await _orderHistoryService.RecordAsync(
			orderId,
			OrderHistoryEventType.ApplicationFormWithdrawn,
			previousStatus,
			OrderStatus.ApplicationWithdrawn,
			cancellationToken,
			OrderHistorySource.PublicApi);
```

**This write is not in a transaction with the `UPDATE`.** If `RecordAsync` throws, the order is already
withdrawn and the caller gets a 500 while the state change stands. That is the opposite ordering from
`InsertEmailInvitationRequestAsync`, which uses `TransactionRunner.RunAsync` precisely to avoid a
half-created order. Here the inconsistency is *tolerable* — a missing timeline entry degrades the
record rather than corrupting it — but it is a deliberate-looking gap worth knowing about. §9.6.

The endpoint has a small oddity:

```csharp
			return Results.Ok(new WithdrawOrderEndpointResponse(result.Success).Success);
```

It constructs the response record and immediately unwraps it, so the wire body is a bare `true` (which
matches the docs sample) and `WithdrawOrderEndpointResponse` is dead code. `.Produces<bool>` documents
the bool, not the record. Harmless, but the record is a trap for anyone who assumes it is the contract.

---

## 4. Where the client id comes from on writes

Every read resolves scope from the token (§2.5). Writes take a different route, and it is worth seeing
both, because they fail differently.

`EndorsementSubmissionService.InsertEmailInvitationRequestAsync` (lines 147-150):

```csharp
		emailInvitationRequest.RequestorId = _currentUser.UserId;
		emailInvitationRequest.ClientId = _currentUser.AtsClientId;
		emailInvitationRequest.Requestor = _currentUser.FullName;
		emailInvitationRequest.HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);
```

`ClientId` is **read straight off the token and never validated**. There is no `?? throw`. If the claim
is absent the column is written `null`, and per §2.6's `ApplyOrderScope` a `null` `ClientId` makes the
order invisible to every scoped caller — including the client who just created it. §9.1.

The bulk path does the same, three lines further down (`EndorsementSubmissionService.cs:255-262`), with
a comment explaining why the values are captured at upload rather than at parse time:

```csharp
		// Captured here, not in the parsing job: that job runs on a Quartz thread with no
		// HttpContext, so ICurrentUser would resolve to null for every row it creates.
		bulkUploadFileDetails.ClientId = _currentUser.AtsClientId;
		bulkUploadFileDetails.UploadedByUserId = _currentUser.UserId;
		bulkUploadFileDetails.Requestor = _currentUser.FullName;
		bulkUploadFileDetails.FileKey = bulkFileKey;
```

and the job honours that, reading everything from the persisted row
(`BulkSubmissionProcessorService.cs:208-216`):

```csharp
						PackageId = file.PackageId,
						SelectPackage = file.PackageType,
						EmailSentStatus = EmailStatus.Pending,
						ApplicationFormStatus = ApplicationFormStatus.Pending,
```

```csharp
						ClientId = file.ClientId,
						RequestorId = file.UploadedByUserId,
						Requestor = file.Requestor,
```

This is the same Quartz-thread hazard the OMS ticketing doc describes, solved the same way: persist the
identity at the boundary where an `HttpContext` still exists.

One rough edge in the same method, `EndorsementSubmissionService.cs:205-208`:

```csharp
		bulkUploadFileDetailsDTO.UploadedByUserId = Guid.Parse(_httpContextAccessor!.HttpContext!
		   .User
		   .FindFirst(ClaimTypes.NameIdentifier)!
		   .Value);
```

Four null-forgiving operators in one expression, resolving a value that `_currentUser.UserId` provides
two lines later. A token missing `ClaimTypes.NameIdentifier` produces a `NullReferenceException` → 500
rather than a 401. Unreachable today (`JWTService.GetClaims` always emits it), but it is the only place
in this feature that reaches into `HttpContext` by hand instead of using `ICurrentUser`.

**A filename collision is a 400.** `InsertBulkSubjectAsync` (lines 221-224):

```csharp
		if (await BulkUploadFileNameExistsAsync(bulkUploadFileDetailsDTO.FileName!, ct))
		{
			throw new BadRequestException("A file with this name has already been uploaded.");
		}
```

scoped per client and per user by `BulkUploadFileNameExistsAsync(fileName, _currentUser.AtsClientId, _currentUser.UserId, ct)`.
For a console user naming files by hand this is a helpful guard. For an integration emitting
`endorsements.csv` from a scheduled job, the second run fails until the name changes. §9.5.

---

## 5. Package and order-type validation — the one service all three paths share

### 5.1 `Services/OrderValidation/OrderInputValidator.cs`

The class comment states the reuse rule this feature is the test case for:

```csharp
/// <summary>
/// Shared by the web console, the public API and the bulk CSV parser so all three agree
/// on what a valid order is. Before this existed each path checked only string length,
/// so any text was accepted as a package or an order type and the mistake only surfaced
/// later — at OMS ticketing, where an unmatched package parks the order with an opaque
/// reason.
/// </summary>
```

Three callers, confirmed: `InsertEmailInvitationRequestAsync:103`, `InsertBulkSubjectAsync:228`, and
the AI assistant. Both order-creation paths in this feature reach it through
`EndorsementSubmissionService`, so a public caller cannot bypass it.

Order type first, because it is free:

```csharp
		// Order type first: it needs no database round trip, so an obviously wrong
		// request fails without one.
		if (OrderType.Normalize(orderType) is not { } normalizedOrderType)
		{
			throw new BadRequestException(
				$"'{orderType}' is not a valid order type. Use one of: {string.Join(", ", OrderType.All)}.");
		}
```

Then the package, against the caller's own entitlements:

```csharp
		if (assignedPackages.Count == 0)
		{
			throw new BadRequestException(
				"No screening package is assigned to this client, so an order cannot be created.");
		}

		var matched = assignedPackages.FirstOrDefault(assigned => string.Equals(
			assigned.PackageName,
			package.Trim(),
			StringComparison.OrdinalIgnoreCase));

		if (matched is null)
		{
			// Naming the client's own packages turns a rejection into something the
			// caller can act on. It discloses nothing they could not already read from
			// GET /packages.
			var available = string.Join(", ", assignedPackages.Select(assigned => assigned.PackageName));

			throw new BadRequestException(
				$"'{package}' is not a package available to this client. Use one of: {available}.");
		}
```

That reasoning is sound *given* `GetAssignedPackagesAsync` returns the caller's own packages. It reads
`_currentUser.AtsClientId`, which is the null hole in §9.1 — and when it is null, the "discloses
nothing they could not already read" justification stops holding, because the list is now every
package on the platform and the error message will print all of them.

The return value is why the DTO write-back in §3.3 works:

```csharp
		// The id is what the order stores; the stored spelling of the name travels with
		// it as a label, so a caller who sent "criminal records check" is echoed the
		// canonical form.
		return new ValidatedOrderInput(matched.PackageId, matched.PackageName, normalizedOrderType);
```

`MaxAssignedPackages = 200` is a ceiling on the entitlement lookup, with the reasoning inline:
`// A client's assigned package list is short; this is a ceiling, not a page size.` A client assigned
more than 200 packages would find the 201st unorderable, and `GetPackagesAsync` would silently paginate
it away.

### 5.2 `Constants/OrderType.cs`

```csharp
	public const string Normal = "Normal";

	public const string Rush = "Rush";

	public static readonly string[] All = [Normal, Rush];
```

Two values. `Normalize` is case- and whitespace-insensitive and returns `null` for anything else —
which is what makes it usable both as a validator predicate and as a canonicaliser. Note that
`IOrderInputValidator`'s XML doc says "Checks that the order type is Rush or Normal" while the
OMS ticketing mapper's `TryResolveTurnAroundTimeId` also understands `All`; adding a third value here
ripples into ticketing. §11.

### 5.3 The bulk job — where the source is read back, and where the header check fails

`BulkSubmissionProcessorService.cs:155-166`, the per-row gate:

```csharp
					// One unusable row must not reject the file: the good rows are still
					// worth creating, and the bad ones are reported back instead of being
					// inserted to fail later at email send or OMS ticketing.
					var (rejectionReason, mobileNumber) = BulkSubjectRowValidator.Validate(row);

					if (rejectionReason is not null)
					{
						rejectedRows.Add(new BulkUploadRejectedRowDTO
						{
							RowNumber = rowNumber,
							Reason = rejectionReason
						});

						continue;
					}
```

and the history write, lines 228-244:

```csharp
				// Bulk orders previously recorded no history at all, so their timelines
				// started blank while single orders showed OrderCreated. The source is
				// taken from the file because this job has no HTTP context to resolve
				// the caller from.
				if (subjects.Count > 0)
				{
					await scope.ServiceProvider
						.GetRequiredService<IOrderHistoryService>()
						.RecordManyAsync(
							subjects.Select(subject => subject.EmailInvitationID).ToList(),
							OrderHistoryEventType.OrderCreated,
							null,
							OrderStatus.PendingCandidateInfo,
							cancellationToken,
							file.Source ?? OrderHistorySource.Web,
							file.UploadedByUserId);
				}
```

`file.Source ?? OrderHistorySource.Web` is the read-back half of the design doc's Step 2: because the
CSV is parsed long after the upload returns, the source has to survive on the row. It is written at
`EndorsementSubmissionService.cs:262-263`:

```csharp
		// Carried on the file so the parsing job can stamp it on every order it creates.
		bulkUploadFileDetails.Source = source;
```

**The header check, and why C2 is wrong.** `BulkSubmissionProcessorService.cs:139-143`:

```csharp
				if (!headersMatchTemplate)
				{
					_logger.LogError("Failed Transaction: Invalid CSV columns for identity: {@Context}. Expected: {ExpectedHeaders}. Actual: {ActualHeaders}", logContext, string.Join(",", expectedHeaders), string.Join(",", actualHeaders));
					throw new InternalServerException("Invalid CSV format. Please use the required column headers.");
				}
```

That throw is caught by the per-file catch at lines 297-305:

```csharp
			catch (Exception ex)
			{
				// Leave the file Pending so the next tick retries it. The catch is here
				// so one failing file does not stop its siblings from being marked Done,
				// which would otherwise re-insert their candidates on the next tick.
				_logger.LogError(ex, "Failed Transaction: Bulk file processing failed, will retry: {@Context}", logContext);

				return (file, succeeded: false);
			}
```

and then released:

```csharp
		if (failedFiles.Count > 0)
		{
			// Release the claim now instead of leaving these files to the sweeper.
			await _repository.ReleaseBulkFileClaimsAsync(failedFiles);
		}
```

Retrying is the right policy for a transient failure — an object-storage timeout, a deadlock. It is
wrong for a malformed header, which will fail identically forever. **Unlike OMS ticketing, which has
`MaxTicketAttempts = 5`, the bulk job has no attempt counter at all.** A file with a bad header row is
re-downloaded from object storage and re-parsed on every tick indefinitely, occupying one of the
`SemaphoreSlim(3)` slots each time, while `GET /endorsements/bulk/{fileId}` reports `Pending` with
`acceptedRowCount: 0`, `rejectedRowCount: 0` and an empty `rejectedRows` — no reason, ever.

Both the design doc ("Not done" 8) and the docs site (`new ApiErrorDoc("400", "Wrong CSV columns", "The
header row must contain exactly the five documented columns. Reported by the status endpoint…")`)
promise the reason surfaces through the status endpoint. It does not. §9.7.

---

## 6. `OrderHistorySource.PublicApi` — what it is and where it is passed

`Constants/OrderHistorySource.cs`, the whole file:

```csharp
namespace ATS.Constants;

public static class OrderHistorySource
{
	public const string Web = "Web";
	public const string PublicApi = "PublicApi";
	public const string System = "System";
}
```

A `public static class` of `public const string`, not an enum — the same choice `TicketStatus` makes,
for the same reason: the value crosses the API boundary as data and is stored as text.

It travels as an **optional trailing parameter defaulting to `Web`** on all three history entry points,
which is why no existing caller had to change (`Services/OrderHistory/IOrderHistoryService.cs:5,11`):

```csharp
	Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web);
```

```csharp
	Task RecordManyAsync(IReadOnlyCollection<Guid> invitationIds, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web, Guid? changedByUserId = null);
```

`IOrderHistoryFactory.Create` carries the same pair, including `changedByUserId` — added because
`OrderHistoryFactory` reads `ICurrentUser`, which is null on a Quartz thread.

**Exactly three production call sites pass `PublicApi`** (verified by grep across the solution; the
other three hits are in `PublicApiRepositoryIntegrationTests.cs`):

| # | Where | What it stamps |
|---|---|---|
| 1 | `Features/PublicApi/CreateEndorsement/CreateEndorsementHandler.cs:91` | the `OrderCreated` entry on a single order |
| 2 | `Features/PublicApi/CreateBulkEndorsement/CreateBulkEndorsementHandler.cs:73` | persisted to `BulkUploadFileDetails.Source`, then read back by the job for every row's `OrderCreated` |
| 3 | `Services/PublicApi/PublicApiService.cs:99` | the `ApplicationFormWithdrawn` entry on a withdraw |

Why it matters: `PublicOrderDetailDTO.History` projects `Source` on every row, so a caller polling
`GET /orders/{orderId}` can see which of their own actions produced which transition — and, more
usefully, so can anyone reading the console's order timeline. It is the only record of *which channel*
raised an order; `ClientId` says who, `Source` says how. Without it, an order created by an integration
is indistinguishable from one a staff member typed in, and a support conversation about "the order that
appeared overnight" has nothing to go on.

**The gap:** the two bulk paths are only traceable because `Source` is persisted on the file. A
withdraw raises its entry directly. But nothing stamps `PublicApi` on the *order* itself — only on
history rows. An order whose every subsequent event came from the console still shows
`Source = "PublicApi"` on its first row, which is the intended reading, but there is no column you can
filter on to answer "which orders came in through the API?" without joining the history table.

---

## 7. Rate limiting

### 7.1 All eight routes carry a policy — verified

`Path/ATSPaths.cs` lines 281-401 define exactly eight `RouteDefinitionDTO`s under the
`// ---- Public API ----` banner, at `MatchPath` lines 283, 298, 314, 329, 344, 359, 374 and 389. Each
one carries:

```csharp
				Metadata: new Dictionary<string, string>
				{
					{ "RateLimitPolicy", GatewayConstants.RateLimitPolicies.DefaultStrict }
				}
```

A grep for `DefaultStrict` across the whole solution returns those eight sites, the constant
declaration, and the `switch` arm that implements it. **There is no unthrottled public route, and
`DefaultStrict` is used by nothing else** — this feature is its first and only consumer, as the design
doc claims.

| RouteId | MatchPath | Method | Transform |
|---|---|---|---|
| `PublicCreateEndorsement` | `/publicapi/ats/endorsements` | POST | `PathSet` |
| `PublicCreateBulkEndorsement` | `/publicapi/ats/endorsements/bulk` | POST | `PathSet` |
| `PublicGetBulkUploadStatus` | `/publicapi/ats/endorsements/bulk/{fileId}` | GET | `PathPattern` |
| `PublicGetPackages` | `/publicapi/ats/packages` | GET | `PathSet` |
| `PublicGetOrders` | `/publicapi/ats/orders` | GET | `PathSet` |
| `PublicGetOrder` | `/publicapi/ats/orders/{orderId}` | GET | `PathPattern` |
| `PublicDownloadReport` | `/publicapi/ats/orders/{orderId}/report` | POST | `PathPattern` |
| `PublicWithdrawOrder` | `/publicapi/ats/orders/{orderId}/withdraw` | PATCH | `PathPattern` |

### 7.2 The bucket is one, and the fix is already in the same file

`ApiGateways/YarpApiGateway/Extensions/GatewayServiceExtensions.cs:101-108`:

```csharp
					GatewayConstants.RateLimitPolicies.DefaultStrict => RateLimitPartition.GetFixedWindowLimiter(policyName, _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 20,
						Window = TimeSpan.FromMinutes(1),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					}),
```

The partition key is `policyName` — the literal string `"DefaultStrict"`, identical for every request.
So this is **one 20-per-minute bucket shared by all eight routes and every client on the platform**.
`QueueLimit = 0` means the 21st request in a window is rejected outright with
`options.RejectionStatusCode = 429`.

Practically: two integrations polling `GET /orders/{id}` every two seconds exhaust the entire public API
for everyone, including each other, and a third client's `POST /endorsements` starts failing with 429.
That is a denial of service one careless integrator can inflict on all the others.

The immediately following arm shows the codebase already knows the correct pattern,
`GatewayServiceExtensions.cs:110-122`:

```csharp
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

*"a single shared bucket would let one caller starve every [other]"* is precisely the `DefaultStrict`
problem, solved one arm later by keying on `RemoteIpAddress`. For an authenticated surface the better
key is the token subject, available at that point as
`httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value`. The design doc's "Not done" item 2
identifies the issue; it does not note that the remedy is already sitting in the same `switch`.

### 7.3 The token endpoint has no policy at all

The rate limiter's fallback, `GatewayServiceExtensions.cs:124-131`:

```csharp
					_ => RateLimitPartition.GetFixedWindowLimiter(GatewayConstants.RateLimitPolicies.Default, _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 500,
						Window = TimeSpan.FromSeconds(1),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					})
```

and the lookup that decides which arm runs (`GatewayServiceExtensions.cs:84-88`):

```csharp
					var md = endpoint.Metadata.GetMetadata<IReadOnlyDictionary<string, string>>();
					if (md is not null && md.TryGetValue("RateLimitPolicy", out var configured))
					{
						policyName = configured ?? "default";
					}
```

No `Metadata` → `policyName` stays `Default` → **500 requests per second**.

`Auth_Login` has no `Metadata` (§1.1). Its sibling does:

```csharp
			new RouteDefinitionDTO(
				RouteId: "LoginWebEntryPoint",
				MatchPath: "/token/web/generatetoken",
				…
				Metadata: new Dictionary<string,string>
				{
					{ "RateLimitPolicy", GatewayConstants.RateLimitPolicies.LoginPolicy }
				}
			),
```

`LoginPolicy` is 5 per 10 seconds. So the browser login is throttled 100× more tightly than the
endpoint this feature's documentation tells integrators to authenticate against. `LoginService` does
have per-account lockout after three failed attempts (`ErrorThreeAttempts`), which bounds credential
stuffing against one *known* username — but at 500/s an attacker can sweep a large username space, and
the lockout itself becomes a denial-of-service lever against any account whose name is guessable. §9.8.

---

## 8. The documentation site

### 8.1 One line makes it public — verified

`UI/FrontendWebassembly/Pages/Docs/ApiDocs.razor:1-13`:

```razor
@page "/docs/api"
@page "/docs/api/{Section}"
@layout GenericLayout
@using FrontendWebassembly.Pages.Docs

@* Public and unauthenticated by design: GenericLayout is the opt-out. App.razor's
   default is MainLayout, whose OnInitializedAsync is the only redirect-to-login in the
   app, so declaring GenericLayout bypasses it. Do not add SecurePageBase or
   RequirePermission here.

   Code blocks and status colours come from the shared .ats-code-block / .ats-json-*
   rules in wwwroot/css/ats.css. Only the three-column shell is scoped to this page. *@
```

`App.razor:3` confirms the default:

```razor
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
```

`GenericLayout.razor` is the entire file — providers, an `ErrorBoundary`, and `@Body`. No auth, no
redirect, no `ICurrentUser`. `MainLayout.razor.cs:55-71` is the gate being bypassed:

```csharp
	protected override async Task OnInitializedAsync()
	{
		try
		{
			var isAuthenticated = await IAuthService.IsAuthenticated();

			if (!isAuthenticated)
			{
				…
				Navigation.NavigateTo("/login");
```

The mechanism claim is exactly right; only the superlative "the only redirect-to-login in the app" is
off (**C9**). `GenericLayout` is the established opt-out, not something invented here —
`ATSApplicationForm.razor`, `VerifyEmployment.razor` and `PhilSysLiveness.razor` all use it.

### 8.2 Content is a typed model

`Pages/Docs/ApiDocsContent.cs:5-47`:

```csharp
public record ApiFieldDoc(string Name, string Type, bool Required, string Description);
```

```csharp
public record ApiErrorDoc(string Status, string Reason, string Detail);
```

```csharp
public record ApiEndpointDoc
{
	public string Anchor { get; init; } = string.Empty;

	public string Title { get; init; } = string.Empty;

	public string Method { get; init; } = string.Empty;

	public string Path { get; init; } = string.Empty;
	…
	public IReadOnlyList<ApiFieldDoc> Fields { get; init; } = [];

	public string CurlSample { get; init; } = string.Empty;

	public string CSharpSample { get; init; } = string.Empty;

	public string ResponseStatus { get; init; } = string.Empty;

	public string ResponseSample { get; init; } = string.Empty;

	public IReadOnlyList<string> Notes { get; init; } = [];

	public IReadOnlyList<ApiErrorDoc> Errors { get; init; } = [];
}
```

with the reasoning for the choice in the class comment:

```csharp
/// The documentation itself, as a typed model rather than markdown files. Compiler-checked
/// and refactor-safe, and it avoids an nginx trap: `.md` has no location block, so a
/// mistyped content path would return index.html with HTTP 200 instead of a 404.
```

**Eight endpoint entries, matching the eight slices exactly** — `create-endorsement`,
`create-bulk-endorsement`, `bulk-status`, `list-orders`, `get-order`, `withdraw-order`,
`download-report`, `list-packages`. Methods and paths agree with the Carter routes in every case.
Nothing is documented that does not exist; nothing that exists is undocumented.

"Compiler-checked" is true of the *model* and not of the *content*: `Method`, `Path` and the samples are
free strings, so a route renamed in `ATSPaths.cs` silently desynchronises the docs. §11.

### 8.3 Two base URLs, and the samples use the wrong one

```csharp
	public const string BaseUrl = "https://oneplatform.cibi.com.ph/";

	public const string TokenPath = "/token/generatetoken";
```

`BaseUrl` is rendered into the Overview callout (`ApiDocs.razor:74-76`:
`All requests go to <code>@ApiDocsContent.BaseUrl</code> over HTTPS`) and into the auth sample
(`ApiDocs.razor.cs:40-45`):

```csharp
	// Doubled $ so the JSON braces in the body are literal and only {{...}} interpolates.
	private static string AuthSample =>
		$$"""
		curl -X POST "{{ApiDocsContent.BaseUrl}}{{ApiDocsContent.TokenPath}}" \
		  -H "Content-Type: application/json" \
		  -d '{ "username": "<your username>", "password": "<your password>" }'
		""";
```

`BaseUrl` ends with `/` and `TokenPath` begins with one, so this renders
`https://oneplatform.cibi.com.ph//token/generatetoken` — a double slash. Tolerable in a URL, sloppy in
the one sample every integrator runs first.

Every other sample hardcodes a **different host**. Sixteen occurrences of
`https://api.cibi.com.ph` across `CurlSample`/`CSharpSample`, against zero uses of `BaseUrl` outside
`AuthSample`. So the page tells the reader "all requests go to `oneplatform.cibi.com.ph`" and then shows
them sixteen requests to `api.cibi.com.ph`. **C4.** The design doc names only the second.

The samples are literal strings by design (`ApiEndpointDoc`'s comment: *"Samples are held as literal
strings rather than generated from the DTOs so the page shows exactly what a caller should send"*),
which is a reasonable trade — but it is why nothing caught this. Interpolating `BaseUrl` into them
would have made the disagreement impossible.

### 8.4 What the docs get right, and the one status they omit

The Authentication section is honest about the mechanism (`ApiDocs.razor:81-96`):

```razor
            <h2>Authentication</h2>
            <p>
                Exchange your credentials for an access token, then send it as a bearer
                token on every request. Tokens expire, so request a new one when you
                receive a <code>401</code>.
            </p>
```

It does not say the credential is a **platform username and password** — the same one a human uses to
sign into the console. An integrator reading this reasonably assumes a scoped API credential. Given
§1.5 (no revocation) and §1.6 (any authenticated user passes), that gap is worth closing before
publication.

The shared status table (`ApiDocsContent.cs:452-464`):

```csharp
	public static IReadOnlyList<(string Code, string Meaning, string Tone)> StatusCodes { get; } =
	[
		("200", "The request succeeded.", "success"),
		("202", "Accepted for processing. Used by the bulk upload, which is parsed in the background.", "success"),
		("400", "The request body failed validation. The response says which field and why.", "warn"),
		("401", "The access token is missing, expired or invalid.", "warn"),
		("404", "The record does not exist, or does not belong to your client.", "warn"),
		("409", "The record exists but is in a state that does not allow this operation.", "warn"),
		("429", "Too many requests. Slow down and retry.", "warn"),
		("500", "Something failed on our side. Safe to retry.", "error")
	];
```

The 404 line correctly documents the §2.4 rule from the caller's side. **403 is missing**, and
`PublicApiService` returns it (§9.2). So is any hint that 429 arrives after 20 requests per minute
*platform-wide* — "slow down and retry" is not actionable advice for a limit the reader is not solely
consuming.

`("500", "… Safe to retry.")` is wrong for the two 500s this surface can actually produce: the
malformed-CSV case (§5.3) will fail identically forever, and `DownloadIndividualReportAsync`'s
storage failure (§6.2) is not idempotent-safe to hammer.

---

## 9. Sharp edges

Ordered by how much they matter. None of these are fixed here.

### 9.1 A token with no `atsClientId` sees — and can order against — every package

The chain, each link verified above:

1. `.RequireAuthorization()` means only "authenticated" (§1.6).
2. `JWTService.GetClaims` emits `atsClientId` **conditionally**, so a user with no ATS access has a
   valid token without it (§1.3). `AtsAccessClaimsProvider` also returns `null` for inconsistent roles
   or an inactive client — a data-entry accident, not a security decision (§1.2).
3. `CurrentUser.AtsClientId` → `ParsePositiveInt(null)` → `null` (§1.4).
4. `GetPackagesHandler` passes that `null` to `GetPackagesAsync` (§3.2).
5. `BuildPackagesQuery` filters only `if (clientId is > 0)` — **`null` means no filter** (§3.2).

Result: `GET /publicapi/ats/packages` with an ATS-less token returns the entire platform package
catalogue, contradicting the endpoint's own `.WithDescription("Returns the background-check packages
the access token's client is entitled to.")`.

The write side is worse, because `OrderInputValidator.GetAssignedPackagesAsync` makes the *same* call:

```csharp
		// The client comes from the caller's token, never from the request, so one
		// client cannot order against another's entitlements.
		var packages = await _packageManagementService.GetPackagesAsync(
			paginationRequest,
			cancellationToken,
			_currentUser.AtsClientId);
```

With `null`, `assignedPackages` is every package, the `Count == 0` guard does not fire, `matched`
resolves for any name sent, and `POST /publicapi/ats/endorsements` succeeds. The order is written with
`ClientId = null` (§4) and `RequestorId = <that user>`, which makes it invisible to every scoped read
including the console's — an orphan that still emails a candidate and still queues for OMS ticketing.

**Neither `GetPackages` nor either create slice calls `IAtsAccessScopeResolver`.** `GetPackagesHandler`
injects `IPackageManagementService` and `ICurrentUser` directly; the create slices reach scope only
through `OrderInputValidator`'s package lookup. So the 403 that protects `GetOrder`,
`GetBulkUploadStatus` and `WithdrawOrder` has no counterpart here.

Three separable fixes, any of which closes most of it: have the three slices resolve scope first and
throw `ForbiddenException` on `null`; make `BuildPackagesQuery` treat `null` as "match nothing" and
give the console's admin screen an explicit unrestricted flag; or reject a create when
`_currentUser.AtsClientId` is null. The middle one is the root cause — `null`-means-everything is a
reasonable convention for an internal admin query and a dangerous default for a parameter that also
guards a public endpoint.

### 9.2 "No ATS access" produces three different status codes

| Route | Path taken | Result for a caller with no ATS access |
|---|---|---|
| `GetOrder`, `GetBulkUploadStatus`, `WithdrawOrder` | `PublicApiService.ResolveScopeAsync` | **403** `"The access token does not grant ATS access."` |
| `GetOrders` | `ReportService.GetReportsAsync:201` | **200** with an empty page |
| `DownloadReport` | `ReportService.DownloadIndividualReportAsync:420` | **404** `"No documents found for email invitation ID …"` |
| `GetPackages`, `CreateEndorsement`, `CreateBulkEndorsement` | no resolver | **200/201-equivalent success** (§9.1) |

Each is defensible in isolation — the console wants an empty list, not an error — but this is one
public surface, and an integrator writing one error handler cannot tell "you have no access" from "no
results" from "you may create orders but not read them". The 403 is undocumented (**C10**), so the most
likely integration behaviour is an unhandled exception in their code.

`ReportService`'s choice is inherited, not chosen: reusing the service is the rule, and the rule brings
the service's error semantics with it. That is the cost of "reuse, do not fork", and it is worth paying
— but it should be a documented cost rather than a surprise.

### 9.3 Internal fields cross the public boundary

`GET /orders` returns `ReportListDTO` verbatim (§3.1), which includes:

- **`Requestor`** — the internal staff member's display name. `PublicOrderDetailDTO` deliberately omits
  it; the list does not.
- **`HitStatus`** — an internal screening outcome with no meaning to an integrator and no entry in the
  docs site's field list.

`GET /packages` returns `PackageDetailsDTO` verbatim (§3.2), which includes:

- **`PackageDescription`** — free text whose leading digits `OMSTicketPayloadMapper.TryParseReportTypeId`
  parses into the legacy OMS `ReportTypeID`. Publishing it hands external callers the internal routing
  code for every package on the platform.
- **`FollowUpEmail`** (an `int` flag, not an address), `CreatedAt`, `UpdatedAt`.

The docs samples show three and four fields respectively — `{ packageId, packageName, isActive }` and
`{ emailInvitationRequestId, subjectName, orderStatus, selectedPackage }` — so the published contract
understates what is actually returned by four and nine properties. An integrator coding against the
docs will not expect the extras; a security reviewer reading only the docs will not see them at all.

The fix is the one `GetOrder` already demonstrates: a `Public*DTO` projection at the boundary. Two of
eight slices have one; six do not.

### 9.4 The HttpOnly cookie silently overrides the bearer header

`OnMessageReceived` assigns `context.Token` unconditionally whenever the cookie is present (§1.4).
`JwtBearerHandler` has already read `Authorization: Bearer …` into that same property, so the header is
discarded without an error or a log line.

Any request carrying both credentials is authenticated as the *cookie's* user. The cookie is set by
`LoginAsync` itself (§1.2), so an integrator who tested their flow in a browser where they were also
signed into the console has been authenticating as their console identity all along — and their
`Authorization` header has been decorative.

Cross-site exploitation is limited by `SameSite = SameSiteMode.Lax` on the cookie (`LoginService.cs:141`)
and by `CookieSecurePolicy.Always`, both of which hold outside Development. The realistic impact is
mis-attribution rather than takeover: orders created during such a test are attributed to the console
user's client, and the behaviour vanishes when the test moves to a real HTTP client. The one-line fix
is to make the assignment conditional — `if (string.IsNullOrEmpty(context.Token) && …)` — so an explicit
bearer header always wins.

### 9.5 Two integration-hostile 4xx/5xx behaviours

**`Location` points at a path the gateway does not serve.** `Results.Accepted($"/api/public/ats/endorsements/bulk/{result.FileId}", …)`
(§3.4) returns the *backend* path. The public prefix is `/publicapi/ats/…`. A caller doing the
idiomatic thing — follow `Location` — gets a 404 from the gateway. The docs site tells them to poll
`/publicapi/ats/endorsements/bulk/{fileId}` and never mentions `Location`, so the sample path works and
the header does not.

**A repeated filename is a hard 400.** `InsertBulkSubjectAsync` refuses any filename already uploaded by
the same user (§4). A scheduled integration writing `endorsements.csv` fails on its second run with
`"A file with this name has already been uploaded."` The console benefits from this guard; an API caller
has no reason to pick human-distinct names. Neither the endpoint's `.WithDescription` nor the docs
site's `Errors` list mentions it.

### 9.6 Withdraw's audit entry is outside the state change

`WithdrawOrderAsync` runs `ExecuteUpdateAsync` and then `RecordAsync` as two independent operations
(§3.7). A failure in the second leaves the order withdrawn with no timeline entry and returns 500 to a
caller whose operation actually succeeded — who will then retry, and get 409.

The module has the right tool: `TransactionRunner.RunAsync` is used by
`InsertEmailInvitationRequestAsync` for exactly this reason, and `RunWithCompensationAsync` by
`InsertBulkSubjectAsync`. Neither is used here. The blast radius is small (a missing history row, not
corrupt state), but the caller-visible outcome — 500 on success, then 409 on retry — is the worst
available combination for an integration to handle.

### 9.7 A malformed CSV retries forever and never reports why

Full trace in §5.3. The header check throws `InternalServerException`; the generic per-file catch marks
the file failed; `ReleaseBulkFileClaimsAsync` returns it to `Pending`; the next tick repeats. There is
no attempt counter, so no terminal state exists. Meanwhile:

- object storage is re-downloaded and the CSV re-parsed every tick, forever;
- one of three `SemaphoreSlim` slots is occupied by a file that can never succeed;
- `GET /endorsements/bulk/{fileId}` returns `Pending` with zero accepted, zero rejected and no reason;
- the docs site promises the reason is "Reported by the status endpoint" (**C2**).

OMS ticketing solved the identical problem with `MaxTicketAttempts = 5` and a non-retryable
`isRetryable: false` classification. The bulk job has no equivalent. A `Failed` status plus a stored
reason on `BulkUploadFileDetails` — the columns for row outcomes already exist — would make this
observable; an attempt cap would make it stop.

### 9.8 The credential endpoint is the least throttled route on the platform

§7.3. `Auth_Login` carries no `RateLimitPolicy` and falls to 500/s, while the browser login beside it is
held to 5/10s. Per-account lockout after three failures bounds guessing against one known username and
does nothing for a username sweep — and turns the endpoint into a lockout lever against any account an
attacker can name. Adding `{ "RateLimitPolicy", GatewayConstants.RateLimitPolicies.LoginPolicy }` to
`Auth_Login`'s (currently absent) `Metadata` is a one-line change, though it would need a separate
policy if 5/10s is too tight for legitimate integrations re-authenticating on 401.

### 9.9 `DownloadIndividualReportAsync` leaks exception text to the caller

`ReportService.cs:464-468`:

```csharp
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to download individual report {@Context}", logContext);
					throw new InternalServerException($"{ex}");
				}
```

`$"{ex}"` is the full `ToString()` — exception type, message and stack trace. On an object-storage
failure that typically includes the provider's error text, which can carry the bucket name, the object
key, the endpoint host and the request id. This is an authenticated external surface, and the module's
own guidance is to throw the typed exception and let `CustomExceptionHandler` produce the body.
`InternalServerException("The requested documents could not be retrieved.")` with the detail left in the
log would lose nothing the caller can act on.

### 9.10 An unscoped repository method with no caller

`PublicApiRepository.cs:145-150`:

```csharp
	public Task<string?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken) =>
		_dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(invitation => invitation.EmailInvitationID == orderId)
			.Select(invitation => invitation.OrderStatus)
			.FirstOrDefaultAsync(cancellationToken);
```

Declared on `IPublicApiRepository.cs:38`, implemented, and **called from nowhere** — a grep across
`BackendAPI` finds only those two lines. It is the only method in the repository that does not take
`authorizedClientIds`/`requiredRequestorId` and does not route through `ApplyOrderScope`.

Dead code today, so not exploitable today. But it sits in the one repository whose entire purpose is
scoped public reads, it is on the public interface, and the next developer adding a status-poll endpoint
will find it, use it, and ship an out-of-scope read that returns another client's order status by id.
Either delete it or make it take the scope parameters like its three siblings.

### 9.11 The storage-key guard is sound

Checked because `docs/reviews/ats-oneplatform-fix-details.md` §3 records a **High** finding here, and
because `DocumentTypes` is caller-supplied. The current code is correct. The review document describes
the before:

```csharp
public class DownloadIndividualDocuments
{
    public string? FileKey { get; set; }   // <-- straight from the browser
    public string? FileName { get; set; }
}
```

> The validator only checked non-empty and ≤255 chars. Any authenticated user could name any object in
> the bucket.

The current DTO has no key field at all (`DTO/DownloadIndividualDocumentsRequestDTO.cs`):

```csharp
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
```

and the resolution is a closed allow-list matched against the *scoped* row (`ReportService.cs:485-512`):

```csharp
	private static IEnumerable<(string FileName, string FileKey)> ResolveRequestedDocuments(
		ReportResultDTO result,
		IReadOnlySet<string> requested)
	{
		var candidates = new (string Type, string? FileName, string? FileKey)[]
		{
			(AtsDocumentTypes.BiometricPhoto, result.BiometricPhotoFileName, result.BiometricPhotoFileKey),
			(AtsDocumentTypes.Resume, result.ResumeFileName, result.ResumeFileKey),
			(AtsDocumentTypes.GovernmentId, result.IdUploadedFileName, result.IdUploadedFileKey),
			(AtsDocumentTypes.NbiClearance, result.NbiClearanceFileName, result.NbiClearanceFileKey),
			…
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

Four properties make this safe, and all four are needed:

1. **The caller's strings are used only as set-membership keys** against `AtsDocumentTypes.All` (11
   constants). A `DocumentTypes` entry that is not one of them matches nothing. There is no path
   construction, no concatenation, no `Path.Combine`, no dictionary lookup on caller text.
2. **Every `FileKey` comes from `result`**, which is the row `GetReportResultByEmailInvitationRequestIdAsync`
   returned *under the caller's scope*. The key space is bounded by what the caller may already read.
3. **Scope is resolved before the key lookup**, `ReportService.cs:417-424`:

   ```csharp
		// This endpoint used to accept object storage keys straight from the caller and
		// hand them to the bucket, which made it a general-purpose read primitive for
		// any authenticated user. Keys are now resolved here, under the caller's scope.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			throw new NotFoundException($"No documents found for email invitation ID {downloadInvididualRequest.EmailInvitationRequestId}.");
		}
   ```

   and an out-of-scope order yields `NotFoundException` — **404, not 403**, per the guide.
4. **`fileName` in the ZIP entry also comes from the row**, not the request, so a caller cannot inject a
   `../../` entry name into the archive either.

The download name is likewise server-derived — `Results.File(result.ZipStream, "application/zip", $"{result.SubjectName}.zip")`
with the endpoint comment *"The archive name is derived server-side from the order the caller was
actually allowed to read, never from anything they supplied."* `SubjectName` falls back to
`"ATS_Documents"` when blank. It does originate from the client's own `FirstName`/`LastName` at creation
time, but it names *their* order, so there is no cross-client angle.

One stale artifact: the review document quotes seven candidate types; the live method has eleven
(`NbiClearance`, `Coe1`, `Coe2`, `Coe3` added). The guard's shape is unchanged.

---

## 10. Wiring — what is registered where

### 10.1 `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`

Endpoints and handlers need nothing (§2.2 — assembly scanning covers both). Only the two new types do:

```csharp
		// An integrating client polls these to watch an order move, so a cached read
		// would report exactly the staleness they are polling to avoid.
		services.AddScoped<IPublicApiRepository, PublicApiRepository>();
		services.AddScoped<IOrderHistoryFactory, OrderHistoryFactory>();
		services.AddScoped<IOrderHistoryService, OrderHistoryService>();
```

(line 95) and

```csharp
		services.AddScoped<IPublicApiService, PublicApiService>();
```

(line 161). The shared pieces the slices lean on:

```csharp
		services.AddScoped<IAtsAccessScopeResolver, AtsAccessScopeResolver>();

		// Shared by the web console, the public API and the bulk parser so all three
		// agree on what a valid package and order type are.
		services.AddScoped<IOrderInputValidator, OrderInputValidator>();
```

(lines 114-118), plus `IEndorsementSubmissionService`, `IReportService` and
`IPackageManagementService` in the same block, and `IAtsAccessClaimsProvider` at line 163.

### 10.2 `IPublicApiRepository` deliberately bypasses the cache decorator

The only `Decorate` call in the file targets the aggregate:

```csharp
	services.AddScoped<IATSRepository, ATSRepository>();
	services.Decorate<IATSRepository, ATSCacheRepository>();
```

Most ATS repositories are registered by forwarding through it —
`AddScoped<IXxxRepository>(provider => provider.GetRequiredService<IATSRepository>())` — and inherit
caching. `IPublicApiRepository` is a **separate class registered directly**, so it is never decorated.
The registration comment and the class comment both say why, in the same words: a client polls these
endpoints to watch an order move, so a cached read reports exactly the staleness the polling exists to
avoid. Same reasoning as `BulkUploadRepository` and `IOMSTicketingRepository`.

**Do not "normalise" this registration to match its siblings** — you would cache a polling endpoint.

### 10.3 Gateway

Eight routes in `Path/ATSPaths.cs:281-401`, discovered by the gateway's module scan
(`GatewayServiceExtensions.cs`, `AddModuleDiscoveryAndReverseProxy`, which scans `typeof(ATSMarker).Assembly`
among others). Rate limiting is global-partitioned and reads `RateLimitPolicy` from route metadata
(§7.2). No auth work in the gateway at all — it forwards, and the backend's JWT handler authenticates.

### 10.4 Strings that must agree with nothing enforcing them

| Value | Copies | Where |
|---|---|---|
| The public prefix | 8 | `ATSPaths.cs` `MatchPath` values |
| The backend prefix | 16 | `ATSPaths.cs` transforms **and** the Carter `MapXxx` literals |
| `RouteId` ↔ `.WithName(...)` | 8 pairs | `ATSPaths.cs` and each `*Endpoint.cs` |
| `"RateLimitPolicy"` | 2 | the metadata key in `ATSPaths.cs` and the `TryGetValue` in `GatewayServiceExtensions.cs:87` |
| `"DefaultStrict"` | 3 | `GatewayConstants.cs:14`, `ATSPaths.cs` ×8, the `switch` arm |
| Base URL | 17 | `ApiDocsContent.BaseUrl` **and** 16 hardcoded sample strings — already disagreed (§8.3) |
| Documented `Path` | 8 | `ApiDocsContent.cs` free strings vs the gateway `MatchPath` values |
| `OrderHistorySource.PublicApi` | 1 const, 3 uses | no enum, so a typo is a silent new vocabulary value |

None of these are compile-checked against each other. The `RouteId`/`.WithName` pair is the one that
fails loudest (YARP throws on a duplicate or missing id at startup); the docs-site strings fail
silently and only in front of a customer.

---

## 11. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| A Carter route literal (`MapGet("api/public/ats/orders")`) | The `PathSet`/`PathPattern` in `ATSPaths.cs` **and** the `Path` string in `ApiDocsContent.cs` | Three independent literals in three projects, none compile-checked (§10.4) |
| A route with `{…}` in it | That the transform is `PathPattern`, not `PathSet` | `PathSet` forwards the literal `"{fileId}"`; the comment at `ATSPaths.cs:313` exists because someone hit this (§2.1) |
| A `RouteId` | The matching `.WithName(...)` | Unenforced pair; YARP fails at startup, but the error names neither file (§10.4) |
| `ApiDocsContent.BaseUrl` | All 16 hardcoded `https://api.cibi.com.ph` sample strings | The constant is used in exactly one place; the samples ignore it (§8.3) |
| Anything about a response shape | The `ResponseSample` string in `ApiDocsContent.cs` | Free text, compiler-checked model but not compiler-checked content (§8.2) |
| `PublicOrderDetailDTO`'s properties | `PublicApiRepository.GetOrderAsync`'s `.Select(...)` and the docs `get-order` sample | Hand-mapped projection; a rename compiles and silently drops the field (§2.6) |
| `ReportListDTO` | `GetOrdersEndpointResponse` and the docs `list-orders` sample | The public list returns the *console* DTO verbatim, internal fields included (§3.1, §9.3) |
| `PackageDetailsDTO` | `GetPackagesEndpointResponse` | Same — and `PackageDescription` carries the OMS report-type code (§3.2, §9.3) |
| `BuildPackagesQuery`'s `if (clientId is > 0)` | `GetPackagesHandler`, `OrderInputValidator.GetAssignedPackagesAsync`, and the console's Package Management screen | `null` currently means "no filter", which is right for the admin screen and wrong for the public endpoint (§3.2, §9.1) |
| `ApplyOrderScope` | `GetOrderAsync` **and** `WithdrawOrderAsync` | One helper, both the read and the write; and the bulk-status method has a hand-copied equivalent that will not follow (§2.6, §3.5) |
| `AtsAccessScopeResolver`'s ladder | All six `ReportService` methods, `PublicApiService`, `OMSTicketingMonitoringService`, `BulkUploadMonitoringService`, `DisputeOrderService`, `DashboardService`, `EndorsementSubmissionService`, `AtsAssistantPlugin` | The comment claiming the inline copies remain is already stale for `ReportService` (§2.5) |
| `OrderHistorySource` constants | The 3 `PublicApi` call sites, `BulkUploadFileDetails.Source` rows already persisted, and any consumer filtering on the string | `public const string`, not an enum — a rename orphans stored rows (§6) |
| `InsertEmailInvitationRequestAsync`'s signature | All **three** callers: web handler, `CreateEndorsementHandler`, `AtsAssistantService` | Optional `source` keeps them compiling, which is how a signature change passes unnoticed (§3.3) |
| `EmailInvitationRequestDTO.OrderId` / `SelectPackage` / `RushNormal` | `CreateEndorsementHandler`'s return | The service mutates the caller's DTO; the handler reads it back afterwards (§3.3) |
| `BulkUploadFileDetailsDTO.FileId` | `CreateBulkEndorsementHandler` | Same write-back pattern (§3.4) |
| `OrderType.All` | `CreateEndorsementCommandValidator`, `EmailInvitationRequestCommandValidator`, `CreateBulkEndorsementCommandValidator`, `OMSTicketPayloadMapper.TryResolveTurnAroundTimeId`, and the UI DTO layer | One vocabulary, five consumers, and ticketing maps each value to a `TurnAroundTimeID` (§5.2) |
| `EmailInvitationRequestCommandValidator`'s rules | `CreateEndorsementCommandValidator` | Hand-copied with a `// Mirrors …` comment as the only link; already drifted on `MiddleInitial` (§3.3) |
| `BulkSubjectRowValidator.NormalizeMobileNumber` | `CreateEndorsementCommandValidator`'s `^\d{11}$` and `OMSTicketPayloadMapper.NormalizePhoneNumber` | Three mobile rules on two public endpoints that currently disagree (§3.4) |
| The bulk job's generic `catch` | `RecordBulkFileRowOutcomeAsync`, and the docs' "Wrong CSV columns" promise | A permanent failure is retried forever and never reported (§5.3, §9.7) |
| `NeedsProjection` handling | `IApplicantSearchProjectionService` | `WithdrawOrderAsync` sets it; nothing in this feature reads it (§3.7) |
| `OnMessageReceived` in `ServiceConfiguration.cs` | Every authenticated route in the platform, not just the public API | Cookie-vs-header precedence is global (§1.4, §9.4) |
| `AuthClaimTypes.AtsClientId` / `AtsRoleId` | `JWTService.GetClaims`, `CurrentUser`, `AtsAccessClaimsProvider`, and every `is > 0` / `is { }` guard | Absent and zero are indistinguishable after `ParsePositiveInt` (§1.3, §1.4) |
| `DefaultStrict`'s `PermitLimit` or partition key | All eight public routes at once | One bucket for the whole surface (§7.2) |
| `AtsDocumentTypes` | `ResolveRequestedDocuments`' `candidates` array and the docs `download-report` entry | A new constant with no candidate row is silently undownloadable (§9.11) |
| `IPublicApiRepository`'s interface | `GetOrderStatusAsync` | Unscoped and uncalled; delete it or add the scope parameters before someone wires it up (§9.10) |

---

## When to update this document

Update it in the same change as the code when you: add, remove or rename a public slice; change a route
literal, transform or `RouteId`; change a response DTO or the scope predicate; change the rate-limit
policy on any public route; change how ATS claims are emitted or read; or edit `ApiDocsContent.cs`. A
companion that drifts is worse than none, because it is read with trust.
