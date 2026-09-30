# Authentication & Session Security — Code Explanation

Companion to [`authentication-session-security.md`](authentication-session-security.md). That document explains
*what* the session hardening does and *why* each rule exists. This one exists so a developer can change login,
logout, JWT claims, refresh rotation, cookies, password recovery or OTP behaviour without opening every file cold —
it walks the real call chains, names the exact method at each hop, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is answered by §13.

> **Read §0 first.** The design doc describes one hardened browser session path. The code contains **three** ways to
> obtain a credential and only one of them is hardened, plus a registration response that hands the caller everything
> needed to skip email verification. Every claim below was verified against the code on branch
> `feature/Update-ReadMe-File`; where the two disagree, this document follows the code.
>
> §11 ranks the security findings, each marked **verified in code** or **plausible, not demonstrated**. That
> distinction is deliberate and should survive any edit. Findings point back to the trace that establishes them
> rather than restating it.

---

## 0. Where the design doc no longer matches the code

| # | `authentication-session-security.md` says | The code actually does |
|---|---|---|
| **C1** | "Tokens are delivered through HttpOnly cookies." "Browser login and refresh no longer return token values." | True for `LoginWebAsync` and refresh. **`LoginService.LoginAsync` still returns the real JWT in `LoginResponseDTO.access_token`** — minted without a `sessionId`, so no `sid`, so `OnTokenValidated` never checks it. It is the documented bearer source for the public API (§4, S3) |
| **C2** | "`AuthSessionValidator` uses the existing local `HybridCache` for 30 seconds" | Locality lives in `AddHybridCaches`' `DefaultEntryOptions.Flags = DisableDistributedCache`. `AuthSessionValidator` passes its **own** options setting only `Expiration`/`LocalCacheExpiration`, so it depends on HybridCache's per-property defaulting rather than anything this repo asserts (§6.1) |
| **C3** | "Auth cookies … prefer `SameSite=Lax`" + follow-up "Add antiforgery protection if deployment requires `SameSite=None`" | The JWT and refresh cookies are `Lax` in **every** environment. The only `SameSite.None` cookie is the **Saml2 sign-in cookie**, and only in Development. The follow-up targets a cookie the Auth paths never produce (§3.5) |
| **C4** | "Five invalid attempts consume the OTP." | Verified. But `ResendOtpAsync` sets `AttemptCount = 0; IsUsed = false`, and `/auth/resend-otp` is **unauthenticated with no throttle**, so the budget is renewable without limit (§7.3, S10) |
| **C5** | "Known and unknown emails return the same success behavior." | Both `return true` on the happy path. A **failed SMTP send throws for a known email only**, and the known path does a network round trip the unknown path skips. Status and timing oracles remain (§7.4) |
| **C6** | Single-use reset tokens | Storage and single-use are correct, but validation and consumption are **two separate reads** with the password write in between, so concurrent replays of one token both succeed (§7.4) |
| **C7** | "Logout has no body." | True at the endpoint. `LogoutDTO(userId, revokeReason)` still exists and `LogoutCommand(LogoutDTO? LegacyRequest = null)` still accepts it — and the integration tests still pass one, hiding that the handler ignores it (§10.3) |
| **C8** | "Refresh keeps the same `sid` but creates a new `jti` and refresh-token hash." | Verified. Unstated: rotation also **overwrites `CreatedAt`** and resets `ExpiresAt` to `UtcNow.AddDays(60)` regardless of remember-me, making every web session a sliding 60-day session (§5.3) |
| **C9** | Checklist: "Raw access/refresh/reset tokens never appear in application logs." | **False.** `LoggingBehavior` logs `{@Request}`/`{@Response}` at Information for every MediatR command: plaintext passwords, raw OTPs, raw reset tokens and issued JWTs reach the log stream. They do *not* reach the PostgreSQL log table (that sink is filtered to `LogEventLevel.Warning`), but the claim is unconditional (§9.1) |
| **C10** | "A cryptographically random raw token is emailed, only its hash is stored" | Verified for reset tokens. **NOT FOUND anywhere in the doc:** `POST /register` returns `OtpVerificationResponse`, which includes `OtpCodeHash` and `PasswordHash`, in its 200 body (§7.1, S1) |
| **C11** | Follow-up: "Add endpoint-level rate limiting for login, registration, OTP resend/verify, forgot password and refresh" | Partly done and unrecorded: `LoginWebEntryPoint` already carries `LoginPolicy` — but that policy partitions on the **policy name**, not the client IP, so it is one global 5-per-10-seconds bucket for every user's web login (§8.2, S5) |
| **C12** | "Auth unit tests cover `sid`/`jti`, atomic rotation calls, cache invalidation, and cookie-only responses." | All four verified. **NOT FOUND:** any test of `AuthSessionValidator` itself, of `OnTokenValidated`, of `OnMessageReceived`, or of any cookie flag. Every Auth integration test dispatches MediatR commands, never HTTP (§10) |
| **C13** | Implies the session cache is tag-managed | `AuthSessionValidator` registers `tags: [SessionTag]` (`"auth-sessions"`) but **nothing ever calls `RemoveByTagAsync(SessionTag)`** — invalidation is per-key only (§6.3) |

---

## 1. The data model

Five tables carry the session lifecycle, all POCOs configured fluently under
`BackendAPI/Modules/Auth/Data/EntityConfiguration/`. **The design doc's "no database migration" claim holds** — `sid`
is `AuthRefreshToken.Id`, which already existed.

### 1.1 `Authusers`

```csharp
	public bool IsActive { get; set; } = true;
	public bool IsApproved { get; set; } = false;
```

Those two defaults are the whole self-service registration gate: `IsActive = true` (can authenticate),
`IsApproved = false` (cannot log in until an administrator flips it). `AuthusersConfiguration` restates both as
`.HasDefaultValue(true)` / `.HasDefaultValue(false)`.

**`Email` has no unique index.** The only index declared is
`builder.HasIndex(u => new { u.LastName, u.FirstName, u.Id });`, commented *"Matches the keyset pagination ordering of
the ATS user directory."* Nothing enforces one row per address (S11).

### 1.2 `AuthRefreshToken` — the session row

`Id (int)`, `UserId (Guid)`, `TokenHash`, `CreatedAt`, `ExpiresAt`, `RevokedAt?`, `RevokedReason?`, `IsActive = true`.
`Id` being an `int` is why `sid` is an integer claim and why `OnTokenValidated` parses it with `int.TryParse`.
`AuthRefreshTokenConfiguration` is three lines: `HasKey`, a `timezone('utc', now())` default on `CreatedAt`,
`IsActive` defaulting true. **No index on `UserId`, none on `TokenHash`.** There is no token history — one row per
browser session, rotated in place — which is exactly why reuse detection is impossible without a schema change (§5.4).

### 1.3 The other three

`OtpVerification` doubles as the *pending registration* record: it holds the name parts and the Argon2 password hash until
the OTP is verified, at which point `VerifyOtpAsync` copies them into a new `Authusers` row. It carries `OtpId`,
`OtpCodeHash`, `IsVerified`, `IsUsed`, `AttemptCount`, `CreatedAt`, `ExpiresAt`, `VerifiedAt`, all with configured defaults.
No index on `Email`. `PasswordResetToken` is `Id (long)`, `UserId`, `TokenHash`, `IsUsed`, `CreatedAt`, `ExpiresAt`,
`UsedAt`; its configuration adds the only relationship in this set —
`HasOne<Authusers>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade)` — and **no index on
`TokenHash`**, the column every reset lookup filters on. `AuthAttempts` is the durable lock row (`UserId`, `Email`,
`Attempts`, `Message`, `CreatedAt`, `LockReleaseAt`), and `AuthAttemptsConfiguration` sets
`builder.HasKey(x => x.UserId);` — **`UserId` is the primary key**, so at most one lock row per user, and
`SaveLockedUserAsync` (a plain `AddAsync`) throws on a second insert rather than upserting. That shape is why
`DeleteLockedUserAsync` is an `ExecuteDeleteAsync` keyed on `UserId` alone.

### 1.4 What is indexed and what is not

| Column | Indexed? | Reached by |
|---|---|---|
| `AuthRefreshToken.TokenHash` | **No** | `FindActiveRefreshTokenByHashAsync` — every refresh and logout |
| `AuthRefreshToken.UserId` | **No** | `RevokeAllSessionsAsync`, `IsUserExistAsync` |
| `PasswordResetToken.TokenHash` | **No** | `GetUserTokenAsync` — twice per reset |
| `OtpVerification.Email` | **No** | every register/verify/resend |
| `Authusers.Email` | **No, and not unique** | `GetUserDataAsync`, `IsUserEmailExistAsync` |
| `Authusers (LastName, FirstName, Id)` | Yes | user-directory keyset pagination only |

Every one is reached by an **unauthenticated** endpoint. That is not an index-tuning nitpick: a sequential scan here is
drivable from `/auth/register` and `/auth/forgot-password/is-change-password-token-valid` at the gateway's default
500-requests-per-second bucket (§8.2, S16).

---

## 2. Token issuance — `Services/Login/JWTService.cs`

### 2.1 Signing, key, lifetime

`GetAccessToken(LoginDTO loginDTO, int? sessionId = null)` reads the whole `Jwt` config section on every call — `Key`,
`Issuer`, `Audience`, `ExpiryInMinutes` — builds `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!))`, and signs a
`SecurityTokenDescriptor` whose only populated fields are
`Subject = new ClaimsIdentity(GetClaims(loginDTO, sessionId))`,
`Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes)`, `Issuer`, `Audience` and
`SigningCredentials = new SigningCredentials(symKey, SecurityAlgorithms.HmacSha256Signature)`.

| Property | Value |
|---|---|
| Algorithm | **HS256** (`SecurityAlgorithms.HmacSha256Signature`) |
| Key source | `Jwt:Key` — a shared symmetric secret from configuration (`JWT__KEY` in every appsettings) |
| Lifetime | `Jwt:ExpiryInMinutes`, re-read from config on **every call** |
| `NotBefore` / `IssuedAt` | Not set — the descriptor leaves them null |
| `ClockSkew` | Not set here; `TimeSpan.Zero` on the validation side (§3.1) |

Because the key is read inside the method but `AddJwtAuthentication` captures `key` **once at startup**, a
configuration reload silently splits issuers from validators until the process restarts.

### 2.2 The claim set, verbatim

From `private IEnumerable<Claim> GetClaims(LoginDTO loginDTO, int? sessionId)`:

```csharp
		var claims = new List<Claim>
		{
			// custom claims used by the app/tests
			new Claim(AuthClaimTypes.UserId, loginDTO.Id.ToString()),
			new Claim(AuthClaimTypes.Email, loginDTO.Email),
			new Claim(AuthClaimTypes.FullName, fullName),

			// The parts as well as the join: callers that must address the user by
			// first/last name separately cannot safely split fullName back apart.
			new Claim(AuthClaimTypes.FirstName, loginDTO.FirstName),
			new Claim(AuthClaimTypes.LastName, loginDTO.LastName),

			// standard claims for interoperability
			new Claim(ClaimTypes.NameIdentifier, loginDTO.Id.ToString()),
		};
```

Names come from `Constants/AuthClaimTypes.cs`: `"userId"`, `"email"`, `"fullName"`, `"firstName"`, `"middleName"`,
`"lastName"`, `"platformRoleId"`, `"atsClientId"`, `"atsRoleId"`. `fullName` is built first, then the parts are emitted
too: `middle` is `loginDTO.MiddleName` trimmed or `string.Empty`, and `fullName` is
`string.Join(' ', new[] { loginDTO.FirstName, middle, loginDTO.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))`.

`loginDTO.PasswordHash` is carried into this method but never emitted — verified by reading every `new Claim(...)` in
the file.

### 2.3 Conditional claims

```csharp
		if (!string.IsNullOrEmpty(middle))
			claims.Add(new Claim(AuthClaimTypes.MiddleName, middle));

		claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
		if (sessionId is > 0)
			claims.Add(new Claim(JwtRegisteredClaimNames.Sid, sessionId.Value.ToString(CultureInfo.InvariantCulture)));
```

Then `claims.AddRange(loginDTO.roleId.Where(roleId => roleId > 0).Distinct().Select(…AuthClaimTypes.PlatformRoleId…))`,
and finally `atsRoleId` / `atsClientId`, each guarded by `is > 0`.

| Claim | Condition | Consequence when absent |
|---|---|---|
| `jti` | **always** | — |
| `sid` | `sessionId is > 0` | `OnTokenValidated` returns early — **the token is unrevocable** (§3.4) |
| `middleName` | non-empty middle name | `ICurrentUser.MiddleName` null; no standard-claim fallback exists |
| `platformRoleId` | one claim per distinct positive role id | `PlatformRoleIds` empty → `IsPlatformSuperAdmin` false |
| `atsRoleId` / `atsClientId` | positive | `AtsAccessScopeResolver` treats the caller as unscoped |

`sessionId is > 0` is a value pattern-match, not a null check: passing `0` yields a sid-less token silently.
`AuthRefreshToken.Id` is an identity column so `0` cannot occur, but the guard is worth knowing about before anyone
reuses it.

**There is no `sub` claim and no `ClaimTypes.Role` claim.** The user id travels as `ClaimTypes.NameIdentifier`, which
`JwtSecurityTokenHandler` outbound-maps to `sub` on write and inbound-maps back on read — that round trip is why
`OnTokenValidated` looks for `ClaimTypes.NameIdentifier` and not `"sub"`. And because `AddJwtAuthentication` sets
`RoleClaimType = ClaimTypes.Role` while `GetClaims` never emits one, **`[Authorize(Roles = …)]` can never match a
platform-issued token.** Every authorisation decision therefore happens in service code via
`ICurrentUser.PlatformRoleIds` / `AtsRoleId` — or not at all (S2).

### 2.4 The three callers

A grep for `GetAccessToken(` across `BackendAPI` returns exactly three production sites:

| Caller | Call | `sid`? | Revocable? |
|---|---|---|---|
| `LoginService.LoginWebAsync` (`LoginService.cs:201`) | `GetAccessToken(userData, session.Id)` | yes | yes |
| `RefreshTokenService.GetNewAccessTokenAsync` (`RefreshTokenService.cs:154`) | `GetAccessToken(loginDTO, storedRefreshToken.Id)` | yes | yes |
| **`LoginService.LoginAsync`** (`LoginService.cs:134`) | `GetAccessToken(userData)` | **no** | **no** |

### 2.5 What reads the claims back — `Shared/Implementations/CurrentUser.cs`

Every property is a `GetClaimValue(...)` call listing a standard claim type first and the custom one second:
`UserId => ParseGuid(GetClaimValue(ClaimTypes.NameIdentifier, AuthClaimTypes.UserId))`, and the same shape for `Email`
(`ClaimTypes.Email`, `AuthClaimTypes.Email`), `FullName` (`ClaimTypes.Name`, `AuthClaimTypes.FullName`), `FirstName`
(`ClaimTypes.GivenName`, `AuthClaimTypes.FirstName`) and `LastName` (`ClaimTypes.Surname`, `AuthClaimTypes.LastName`).
`MiddleName` is the exception — `GetClaimValue(AuthClaimTypes.MiddleName)`, one argument.

`GetClaimValue` walks its arguments in order and returns the first non-blank hit, so the **standard claim type is tried
first and the custom one is the fallback**. For platform-issued tokens the first lookup always misses (`GetClaims`
emits none of `ClaimTypes.Email`, `.Name`, `.GivenName`, `.Surname`), so the fallback always wins. It would matter for
a token issued by another party — notably SAML, where an external IdP commonly does emit `GivenName`/`Surname`.
`MiddleName` has no fallback at all.

Role parsing is strict: `ParsePositiveInt` uses
`int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0`, so `" 1"`, `"+1"`
and `"1,0"` all become null rather than a role id. `IsPlatformSuperAdmin` is
`PlatformRoleIds.Contains(Auth.Constants.PlatformRoleIds.SuperAdmin)`, and that constant is the single
`public const int SuperAdmin = 1;` in `Constants/PlatformRoleIds.cs`.

---

## 3. Token transport — `BackendAPI/API/APIs/ServiceConfig/ServiceConfiguration.cs`

All of this lives in `AddJwtAuthentication(IConfiguration, IHostEnvironment)`, called once from `Program.cs`. It
configures **three** schemes in one chain: `AddJwtBearer`, `AddCookie(_signinScheme)` and `AddSaml2`.

### 3.1 Validation parameters

```csharp
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

`ClockSkew = TimeSpan.Zero` is the notable one: the default is five minutes, so an access token dies exactly at
`Expires` with no grace. Since `Jwt:ExpiryInMinutes` drives both the token's `Expires` and the access **cookie's**
`Expires` (§5.2), the two expire at the same instant and the cookie never outlives a token the validator would reject.

`ValidateIssuerSigningKey = true` with one symmetric key and no `ValidAlgorithms` restriction. There is no `alg=none`
exposure (a `none` token has no key to validate against) and no second key to substitute, so this is not an
algorithm-confusion opening **as configured today** — it becomes one if an asymmetric key is ever added without pinning
`ValidAlgorithms`.

### 3.2 `OnMessageReceived` — the cookie is the primary transport

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

`_httpCookieOnlyKey` is `configuration.GetValue<string>("HttpCookieOnlyKey")` — the **access-token** cookie name, read
once at startup. The refresh cookie (`AuthWeb:AuthWebHttpCookieOnlyKey`) is never consulted here;
`RefreshTokenService` reads it directly (§5.3).

### 3.3 Cookie vs `Authorization: Bearer` — the real precedence

The handler assigns `context.Token` **only inside the `TryGetValue` branch**. When the cookie is absent it leaves
`context.Token` untouched, and `JwtBearerHandler.HandleAuthenticateAsync` then falls back to reading
`Authorization: Bearer …` itself. (That fallback is framework behaviour in
`Microsoft.AspNetCore.Authentication.JwtBearer`, not code in this repo — stated here, not quoted.)

1. **Cookie present → the cookie wins.** The event has already assigned `context.Token`, and the handler reads the
   header only when the token is still empty. A bearer header sent alongside a cookie is ignored, with no error and no
   log line.
2. **Cookie absent → the bearer header is used.** Not theoretical: it is the documented public-API transport.
   `UI/FrontendWebassembly/Pages/Docs/ApiDocsContent.cs` ships eight `-H "Authorization: Bearer $TOKEN"` curl samples,
   and `$TOKEN` comes from `POST /token/generatetoken` → `LoginAsync` → `LoginResponseDTO.access_token` (§4).

**Correction to `docs/features/ats-public-api/ats-public-api_code_explanation.md` §9.4.** That section states
*"`JwtBearerHandler` has already read `Authorization: Bearer …` into that same property, so the header is discarded"*
— the ordering is the reverse. Its conclusion (cookie wins when both are present) is right; its mechanism is not, and
the one-line fix it proposes, `if (string.IsNullOrEmpty(context.Token) && …)`, would be **inert**, because
`context.Token` is always null when `OnMessageReceived` runs. The same section credits the JWT cookie with
`CookieSecurePolicy.Always`; that policy is on the **Saml2 sign-in cookie** only — the JWT and refresh cookies use
`Secure = _isHttps`, which is configuration-driven (§5.2, S14).

### 3.4 `OnTokenValidated` — session liveness

```csharp
				OnTokenValidated = async context =>
				{
					var sessionClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
					if (string.IsNullOrWhiteSpace(sessionClaim))
						return; // API/SSO tokens without a browser refresh session keep their existing behavior.

					var userClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
					if (!int.TryParse(sessionClaim, out var sessionId) || !Guid.TryParse(userClaim, out var userId))
					{
						context.Fail("Invalid authentication session.");
						return;
					}

					var validator = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionValidator>();
					if (!await validator.IsActiveAsync(sessionId, userId, context.HttpContext.RequestAborted))
						context.Fail("Authentication session is no longer active.");
				}
```

- **The early return on a missing `sid`** is a compatibility hatch and the most important line in this file. It exists
  so tokens minted by `LoginAsync` — and any externally-issued token — keep working. Its cost is that **absence of
  `sid` is a complete bypass of session liveness**: such a token cannot be revoked before its natural expiry by
  anything in this codebase. `sid` is attacker-invisible but not attacker-removable (it is inside the signed payload),
  so the bypass is only reachable by *being issued* a sid-less token — which the platform does on demand at
  `/token/generatetoken`.
- **`Guid.TryParse(userClaim, …)`** binds the session row to the token's subject; the matching check is validator-side,
  `session.UserId == userId` (§6.1). Without it, a row id reused by another user's session would validate.
- **`int.TryParse(sessionClaim, …)`** fails closed on a non-integer `sid` — `AuthRefreshToken.Id` is an `int`, so
  `"99999999999"` would otherwise overflow.
- **`context.Fail(...)`** rather than throwing: the request becomes unauthenticated (401 via the challenge scheme), not
  a 500.
- **`context.HttpContext.RequestServices`** rather than a captured service: the validator is scoped and
  `AddJwtAuthentication` runs once at startup. Capturing it would root one scope for the process lifetime.

### 3.5 The Saml2 sign-in cookie

`AddCookie(_signinScheme!, …)` sets `Cookie.Name = _signinScheme`, `HttpOnly = true`, `Path = "/"`,
`ExpireTimeSpan = TimeSpan.FromHours(8)`, `SlidingExpiration = true`, then branches on environment. **Both** branches
set `options.Cookie.SecurePolicy = CookieSecurePolicy.Always;`; they differ only in `SameSite` — `SameSiteMode.None`
under `if (environment.IsDevelopment())` (commented `// Development/Ngrok settings`), `SameSiteMode.Lax` otherwise,
beside a commented-out `// options.Cookie.Domain = ".yourdomain.com";`.

This is a **separate credential from the JWT cookie** and the two are never bridged:

| | JWT / refresh cookies | Saml2 sign-in cookie |
|---|---|---|
| Set by | `LoginService` / `RefreshTokenService` | `SSOLoginCallbackHandler.SignInAsync` |
| `Secure` | `_isHttps` from config | `CookieSecurePolicy.Always` (hardcoded) |
| `SameSite` | `Lax`, all environments | `None` in Development, `Lax` elsewhere |
| Lifetime | `Jwt:ExpiryInMinutes` / refresh expiry | 8 hours, sliding |
| Backing row | `AuthRefreshToken` | none |
| Revocable | yes, via `sid` | no |
| Authenticates `[Authorize]` | **yes** (default scheme) | **no** |

That last row is load-bearing. `AddAuthentication` sets
`DefaultAuthenticateScheme = DefaultChallengeScheme = DefaultScheme = JwtBearerDefaults.AuthenticationScheme`, and
`services.AddAuthorization()` registers no policies. A grep for `RequireAuthorization` across `BackendAPI` returns
**111 sites, every one parameterless**, and there is no `AddPolicy` anywhere — so the default policy authenticates
against JwtBearer alone and **the SAML cookie authenticates nothing protected**. See §8.4 for what the SSO path *does*
grant.

### 3.6 `AddSaml2`

`SPOptions.EntityId = new EntityId(spBaseUrl)` and `SPOptions.ReturnUrl = new Uri(spBaseUrl + "/sso/login/callback")`,
with one `IdentityProvider` built from `idpEntityId` and configured `MetadataLocation = idpMetadataUrl`,
`LoadMetadata = true`, **`AllowUnsolicitedAuthnResponse = true`**. The three values come from `Saml2:SpBaseUrl`,
`Saml2:IdpMetadataUrl` and `Saml2:IdpEntityId`. `services.AddOptions<Saml2Options>().ValidateOnStart();` means a
malformed option fails the boot rather than the first assertion.

`AllowUnsolicitedAuthnResponse = true` accepts an IdP-initiated response with no matching `InResponseTo` — the platform
accepts an assertion nobody asked for. **Plausible, not demonstrated:** that is the standard precondition for SAML
login-CSRF / session fixation. I found no relay-state or `Notifications` handler here that would compensate, and whether
IdP-side binding makes it exploitable is not determinable from this codebase. What *is* determinable is that the blast
radius is small, because the resulting cookie authenticates no API endpoint (§3.5). `WantAssertionsSigned`,
signing-certificate selection and `Notifications` are not configured; `LoadMetadata = true` with `MetadataLocation`
means signing certificates come from the IdP metadata document. I did not verify Sustainsys' defaults for the unset
properties and make no claim about them.

---

## 4. Full trace — the sid-less door: `POST /token/generatetoken`

Traced first because it is the weakest of the three credential paths and the design doc does not describe it as live.

**Gateway** — `Modules/Auth/Path/AuthPaths.cs`, first route in `GetRoutes()`: `RouteId: "Auth_Login"`,
`MatchPath: "/token/generatetoken"`, `PathSet → "/login"`, POST only, and **no `Metadata`** — so no `RateLimitPolicy`,
so it falls to the gateway's `default` bucket at **500 requests per second**
(`ApiGateways/YarpApiGateway/Extensions/GatewayServiceExtensions.cs`).

**Endpoint** — `Features/Login/LoginEndpoint.cs`: `app.MapPost("login", …)`, no `.RequireAuthorization()`, no
validator. Builds `LoginCommand(request.username, request.password)`, returns
`Results.Ok(loginResponse.LoginResponseDTO)`.

**Pipeline** — `ValidationBehavior<,>` finds no `AbstractValidator<LoginCommand>` (there is none in the folder);
`LoggingBehavior<,>` logs the whole command (§9.1); `LoginHandler` → `LoginService.LoginAsync(username, password)`.

**Service**, in order: `GetUserDataAsync(new LoginWebCred(username, password, false))` (which filters
`user.Email == cred.Username && user.IsActive == true`, left-joins `AuthUserAppRoles`, projects a `LoginDTO`,
`.AsNoTracking().FirstOrDefaultAsync()`) → `NotFoundException("Invalid username or password.")` if null → lockout
check (§8.1) → `_passwordHasherService.VerifyPassword(userData.PasswordHash, password)` → on failure increment the
counter, re-check the lock, throw the same `NotFoundException` → on success with `currentAttempts > 0` call
`RemoveAttempts` → `IsApproved == false` throws `UnauthorizedAccessException("Your account has not been approved
yet…")` → `AddAtsClaimsAsync(userData)` → **`this._jWTService.GetAccessToken(userData)` with no `sessionId`** →
append the access cookie → return `new LoginResponseDTO(userData.Id.ToString()!, jwtToken, "bearer", …)`, whose second
positional parameter is `string access_token`.

**No `AuthRefreshToken` row is created anywhere in this method** — a grep for `SaveRefreshTokenAsync` finds exactly one
caller, `LoginWebAsync`. Three consequences, all verified:

- No `sid` → `OnTokenValidated` returns at its first line → the session is not re-checked against any row, because
  there is no row.
- **Nothing to revoke.** `LogoutAsync` works off the refresh cookie, which this path never sets.
  `RevokeAllSessionsAsync` (password reset) walks `AuthRefreshToken` rows and cannot touch it either. The token is
  valid until `Expires`, full stop. `IsActive == false` on the user *is* honoured, but only at issuance.
- The token is **JavaScript-readable** wherever this endpoint is called from a browser, because it is in the response
  body. The design doc's "Browser token exposure" row says browser login no longer returns token strings; this endpoint
  still does.

`LoginIntegrationTests.Login_ShouldReturnSuccess_WhenCredentialsAreCorrect` pins it:
`result.loginResponseDTO.access_token.Should().NotBeNullOrEmpty();`.

**Why it exists:** it is the credential source for the public API. `ats-public-api_code_explanation.md` §7.3 already
records that `Auth_Login` is the least-throttled route in the platform. What it does not connect is that the token this
route mints is also the one credential the platform issues that cannot be revoked. Both facts come from the same
`GetAccessToken(userData)` call.

---

## 5. Full trace — browser login, refresh, logout

### 5.1 `POST /token/web/generatetoken` → `LoginWebAsync`

Gateway route `LoginWebEntryPoint` (`/token/web/generatetoken` → `PathSet /loginweb`), the **only** Auth route with
metadata: `Metadata: new Dictionary<string,string> { { "RateLimitPolicy", GatewayConstants.RateLimitPolicies.LoginPolicy } }`.
`Features/LoginWeb/LoginWebEndpoint.cs` → `LoginWebCommand(request.loginWebCred)` → `LoginService.LoginWebAsync(cred)`.
Steps 1–6 are identical to §4. Then two gates `LoginAsync` does not have:

```csharp
		if (!userData.IsApproved)
			throw new UnauthorizedAccessException("Your account has not been approved yet. Please contact an administrator for assistance.");
		if (!userData.AppId.Any() || !userData.SubMenuId.Any() || !userData.roleId.Any())
			throw new UnauthorizedAccessException("Your account has no assigned application. Please contact an administrator for assistance.");
```

The second is why a freshly self-registered account cannot reach the console even after approval: `VerifyOtpAsync`
creates an `Authusers` row with **no** `AuthUserAppRoles` rows (§7.1), so all three collections are empty until an
administrator grants them. Then the session is created — the order is the point:

```csharp
		userData = await AddAtsClaimsAsync(userData);
		var (refreshToken, hashRefreshToken) = _refreshTokenService.GenerateRefreshToken();
		var refreshExpiry = GetRefreshTokenExpiry(cred.IsRememberMe);
		var session = await _authRepository.SaveRefreshTokenAsync(userData.Id, hashRefreshToken, refreshExpiry);
		var jwtToken = _jWTService.GetAccessToken(userData, session.Id);
		SetAccessTokenCookie(jwtToken);
		SetRefreshTokenCookie(refreshToken, refreshExpiry);
```

`SaveRefreshTokenAsync` runs **before** `GetAccessToken`, because the row's identity-generated `Id` *is* the `sid`. It
adds and saves unconditionally — no lookup for an existing row, no reuse of a presented cookie. That is what makes each
browser an independent session, and why logging out one leaves the others alive.

`GetRefreshTokenExpiry(bool isRememberMe)` returns `isRememberMe ? DateTime.UtcNow.AddDays(_cookieExpiryinDaysKey) :
DateTime.UtcNow.AddDays(_httpCookieOnlyRefreshTokenInDays)`, where `_cookieExpiryinDaysKey` is
`GetValue<int>("AuthWeb:CookieExpiryInDayIsRememberMe")` and `_httpCookieOnlyRefreshTokenInDays` is
`GetValue<int>("AuthWeb:AuthWebHttpCookieOnlyDays", 60)`. **`AuthWeb:AuthWebHttpCookieOnlyDays` appears in no
`appsettings.*.json`** — a grep for `AuthWeb` across `BackendAPI/API` returns `CookieExpiryInMinutes`,
`CookieExpiryInDayIsRememberMe`, `AuthWebHttpCookieOnlyKey`, `IsHttps`, `AccountLockDurationInMinutes`,
`MaxFailedAttemptsBeforeLockout`, and not that key. So the non-remember-me branch always resolves to the default **60
days**, while remember-me resolves to whatever the env var holds — `7` in the unit-test fixture. If production is
configured the same way, **ticking "remember me" produces a shorter session than leaving it unticked** (S9).

The response body carries `string.Empty, string.Empty` for both tokens but **does** carry `Appid`, `SubMenuid` and
`RoleId`. Those are not secrets — they are in the signed JWT anyway — but the UI builds navigation from the body rather
than the token, so a stale body and a fresh cookie can disagree after a role change until next login.

### 5.2 Cookie flags — four hand-synced copies

`LoginService.SetAccessTokenCookie`, `LoginService.SetRefreshTokenCookie`, and `RefreshTokenService`'s two methods of
the same names all build the same shape:

```csharp
			var cookieAccessTokenOptions = new CookieOptions
			{
				HttpOnly = true,
				Secure = _isHttps,
				SameSite = SameSiteMode.Lax,
				Expires = DateTime.UtcNow.AddMinutes(_expiryinMinutesKey),
				Path = "/"
			};
```

`_isHttps` is `_configuration.GetValue<bool>("AuthWeb:isHttps")` in `LoginService` and
`bool.Parse(_configuration.GetSection("AuthWeb:isHttps").Value!)` in `RefreshTokenService`. Same key, two failure modes
when it is missing: `GetValue<bool>` yields `false`; `bool.Parse(null!)` throws in the constructor, so
`RefreshTokenService` cannot be resolved at all. **`Secure` is configuration-driven, not derived from the request** —
see S14 for why that matters here.

`Path`, `SameSite` and `HttpOnly` must agree across all four copies or the browser treats login and refresh as
different cookies; `RemoveAccessAndRefreshTokenCookie` calls `Response.Cookies.Delete(...)` with no `CookieOptions` at
all, relying on the browser to match the original `Path`. Nothing enforces any of it (§13).

### 5.3 `POST /token/web/getnewaccesstoken` → refresh rotation

`Features/GetNewAccessToken/GetNewAccessTokenEndpoint.cs` → `RefreshTokenService.GetNewAccessTokenAsync()`. No
`RequireAuthorization`, no validator — the refresh cookie is the credential. It reads
`_httpContextAccessor.HttpContext?.Request.Cookies[_refreshTokenKey]`; a blank value logs `"Refresh token cookie is
missing {@Context}"` and throws `UnauthorizedAccessException("Invalid refresh token.")`. Otherwise it hashes and looks
up via `FindActiveRefreshTokenByHashAsync(HashToken(rawRefreshToken))`, which folds active-ness and expiry into the
query — `.FirstOrDefaultAsync(token => token.TokenHash == tokenHash && token.IsActive && token.ExpiresAt > DateTime.UtcNow)`.

Note what that is **not**: not a constant-time comparison, but a SQL equality predicate on the SHA-512 hash, evaluated
by PostgreSQL. Acceptable here and only here because the preimage is 64 bytes from `RandomNumberGenerator` (§5.5) —
there is no dictionary to time against. Contrast the OTP path, where the secret is six digits and the comparison *is*
`FixedTimeEquals` (§7.2).

Then the rotation: `refreshExpiry` is recomputed as `DateTime.UtcNow.AddDays(_httpCookieOnlyRefreshTokenInDays)` and
passed to `RotateRefreshTokenAsync(storedRefreshToken.Id, refreshTokenHash, newRefreshTokenHash, refreshExpiry, …)`. A
`false` result logs `"Failed to update refresh token for user: {@Context}"` and throws
`UnauthorizedAccessException("Refresh token was already used or is no longer active.")`. On success it calls
`await _sessionValidator.InvalidateAsync(storedRefreshToken.Id);` then
`this._jWTService.GetAccessToken(loginDTO, storedRefreshToken.Id)`.

`RotateRefreshTokenAsync` is the atomicity the design doc describes — one conditional statement, so only one of two
concurrent requests can win:

```csharp
		var updated = await _dbcontext.AuthRefreshToken
			.Where(session => session.Id == sessionId && session.TokenHash == currentHash && session.IsActive && session.ExpiresAt > DateTime.UtcNow)
			.ExecuteUpdateAsync(update => update
				.SetProperty(session => session.TokenHash, replacementHash)
				.SetProperty(session => session.CreatedAt, DateTime.UtcNow)
				.SetProperty(session => session.ExpiresAt, expiryDate), cancellationToken);
		return updated == 1;
```

Three things this reveals that the design doc does not:

- **`CreatedAt` is overwritten.** The row stops recording when the session was created and starts recording when it was
  last rotated. Any future "sessions older than N days" audit or cleanup query built on `CreatedAt` would silently
  measure the wrong thing.
- **`ExpiresAt` is reset to `UtcNow.AddDays(60)` on every rotation**, from
  `RefreshTokenService._httpCookieOnlyRefreshTokenInDays` (`AuthWeb:HttpCookieOnlyRefreshTokenInDays`, default 60, and
  — like its `LoginService` counterpart — absent from every appsettings). A session used at least once every 60 days
  never expires, and a short remember-me session is extended to 60 days on its first refresh.
- **`sid` is preserved by design**: `GetAccessToken(loginDTO, storedRefreshToken.Id)` reuses the row id while
  `GetClaims` regenerates `jti`. `InvalidateAsync` runs *before* the new token is minted. In practice the rotation does
  not change what `IsActiveAsync` would return — same row, still active — so the invalidation is mainly about
  `ExpiresAt` freshness.

### 5.4 No reuse detection — and why

The old hash is **overwritten**, not appended. A replayed old token therefore fails the `TokenHash == currentHash`
predicate and looks identical to a token that never existed: `FindActiveRefreshTokenByHashAsync` returns null and the
caller gets `UnauthorizedAccessException("Invalid refresh token.")`. The service cannot distinguish "replayed after
rotation" from "garbage", so it cannot revoke the family. The design doc says this plainly — *"full historical reuse
detection still requires a future token-history or token-family migration because the current row overwrites the old
hash"* — and the code agrees. What it does buy is the race guarantee: two simultaneous presentations of one token
produce exactly one success and one `UnauthorizedAccessException`, which is what
`RefreshTokenServiceTests.GetNewAccessTokenAsync_ShouldThrowUnauthorized_WhenAtomicRotationLosesRace` pins.

### 5.5 Refresh-token generation and hashing

`GenerateRefreshToken()` fills `new byte[64]` from `RandomNumberGenerator.Create()`, renders it
`Convert.ToBase64String(randomNumber).TrimEnd('=').Replace('+','-').Replace('/','_')`, and returns the token plus
`HashToken(token)` — which is `SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(token))` rendered
`Convert.ToBase64String(hashBytes)`. So: 512 bits of CSPRNG output, base64url-encoded, stored as **unsalted SHA-512 in
standard base64**. For a 512-bit random value that is fine — nothing to precompute against. Note the encoding difference
from `HashService.Hash`, which produces base64**url**: two SHA-512 helpers in one platform, two alphabets, and a value
stored through one will never match a lookup through the other (S15).

`ValidateHashToken` exists and is correct — `FixedTimeEquals` over the decoded bytes, with a `WebUtility.UrlDecode` on
the input — but has **no production caller**; only
`RefreshTokenServiceTests.ValidateHashToken_ShouldReturnTrue_ForUrlEncodedProvidedToken`. The live path uses SQL
equality instead. `RevokeTokenAsync` is `throw new NotImplementedException();` on the public `IRefreshTokenService`
contract.

### 5.6 `POST /auth/logout`

The endpoint takes no body — `app.MapPost("logout", async (ISender sender, CancellationToken cancellationToken) => …)`
building `new LogoutCommand()` — and `LogoutHandler` discards its `request` entirely, calling
`_loginService.LogoutAsync()`.

```csharp
	public async Task<bool> LogoutAsync()
	{
		var rawRefreshToken = GetRefreshTokenFromCookie();
		try
		{
			if (!string.IsNullOrWhiteSpace(rawRefreshToken))
			{
				var session = await _authRepository.FindActiveRefreshTokenByHashAsync(_refreshTokenService.HashToken(rawRefreshToken));
				if (session is not null)
				{
					await _authRepository.UpdateRevokeReasonAsync(session, "UserLogout");
					await _sessionValidator.InvalidateAsync(session.Id);
				}
			}
		}
		finally
		{
			RemoveAccessAndRefreshTokenCookie();
		}
		…
		return true;
	}
```

Every property the design doc claims is here: the credential comes from the server-owned cookie, user and reason come
from the row (`"UserLogout"` is a literal, not client input), cookie deletion is in `finally` so it happens even when
the DB write throws, and the method returns `true` on every path — idempotent by construction, since a missing or
already-revoked token just skips the `if`. `UpdateRevokeReasonAsync` sets all three revocation fields together
(`RevokedReason`, `IsActive = false`, `RevokedAt = DateTime.UtcNow`); the entity came from a tracking query, so its
explicit `Update` call is redundant but harmless.

**Logout only revokes the session it can find.** A §4 token has no refresh cookie and no row, so logout does not affect
it. Nor does it affect other browsers — the intended multi-session behaviour, and the reason `RevokeAllSessionsAsync`
exists separately for password reset.

---

## 6. Session liveness — `Services/RefreshTokens/AuthSessionValidator.cs`

### 6.1 The whole file

```csharp
public sealed class AuthSessionValidator : IAuthSessionValidator
{
	private const string SessionTag = "auth-sessions";
	private readonly IRefreshTokenRepository _repository;
	private readonly HybridCache _cache;

	public AuthSessionValidator(IRefreshTokenRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	public async Task<bool> IsActiveAsync(int sessionId, Guid userId, CancellationToken cancellationToken = default)
	{
		var session = await _cache.GetOrCreateAsync(
			$"auth-session:{sessionId}",
			async token => await _repository.GetSessionAsync(sessionId, token),
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(30), LocalCacheExpiration = TimeSpan.FromSeconds(30) },
			tags: [SessionTag],
			cancellationToken: cancellationToken);

		return session is not null && session.UserId == userId && session.IsActive && session.ExpiresAt > DateTime.UtcNow;
	}

	public ValueTask InvalidateAsync(int sessionId, CancellationToken cancellationToken = default) =>
		_cache.RemoveAsync($"auth-session:{sessionId}", cancellationToken);
}
```

**"Active" is four conditions**, and only one of them is cached. `ExpiresAt > DateTime.UtcNow` is evaluated **on every
call against the current clock**, and `UserId == userId` is compared against the token's `sub` on every call — neither
is baked into the entry. Only `IsActive` is genuinely as-of-the-snapshot.

That is the right factoring and easy to break: moving the whole predicate inside the factory would freeze the user and
expiry checks for 30 seconds too.

### 6.2 Does it hit the database on every request?

No — and the cache is not doubled up. `AuthSessionValidator` depends on `IRefreshTokenRepository`, which
`AuthServiceConfiguration` resolves by forwarding to the **decorated** aggregate
(`services.Decorate<IAuthRepository, AuthCacheRepository>();` then
`services.AddScoped<IRefreshTokenRepository>(provider => provider.GetRequiredService<IAuthRepository>());`). So the
call does pass through `AuthCacheRepository` — but that decorator adds no caching for this method
(`Data/Cache/RefreshTokens/AuthCacheRepository.RefreshTokens.Cache.cs`):

```csharp
	public Task<AuthRefreshToken?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default) =>
		_authRepository.GetSessionAsync(sessionId, cancellationToken);
```

and the repository is a plain `AsNoTracking` primary-key lookup. Net: **one PostgreSQL read per `sid` per 30 seconds
per instance.** Everything else in the `RefreshTokens` decorator is likewise a pass-through — `GetNewUserDataAsync`,
`SaveRefreshTokenAsync`, `RotateRefreshTokenAsync`, `UpdateRevokeReasonAsync`, `FindActiveRefreshTokenByHashAsync`.
**No refresh-token read is cached at the decorator level**, which is correct: a cached `TokenHash` would let a rotated
token keep working.

### 6.3 Invalidation lag

`InvalidateAsync` has exactly three callers — `LoginService.LogoutAsync`,
`RefreshTokenService.GetNewAccessTokenAsync`, and `ForgotPasswordService.ResetPasswordAsync` (once per revoked session
id). It is `_cache.RemoveAsync(key)`, **local to the instance that runs it**. `AddHybridCaches` supplies the defaults:
`Expiration` and `LocalCacheExpiration` both `TimeSpan.FromMinutes(10)`, and
`Flags = HybridCacheEntryFlags.DisableDistributedCache`.

Two observations. First, **`AuthSessionValidator` does not restate `Flags`.** HybridCache defaults unset per-call
properties from `DefaultEntryOptions`, so `DisableDistributedCache` should carry over and the entry should be L1-only —
which is what the design doc asserts. I could not verify that merging rule from anything in this repository, and the
`TairRedis` connection string *is* configured via `AddStackExchangeRedisCache`, so an L2 exists and would be used if
`Flags` did not carry over. **Recorded as C2 rather than asserted either way** — and note the alternative is *better*,
since `RemoveAsync` would then propagate. Either way the window is bounded at 30 s.

Second, **`SessionTag` is registered and never used.** No `RemoveByTagAsync("auth-sessions")` exists anywhere in the
solution, unlike `AuthCacheRepository`, which uses `RemoveByTagAsync` for `LockedUsersTag`, `UnApprovedUsersTag` and
others. So there is no way to drop every session entry at once; a global "revoke everything" operation would have to
enumerate row ids.

**The lag, concretely:** single instance → revocation takes effect on the next request, because `InvalidateAsync` runs
before the response is sent. Multiple instances → up to **30 seconds** on every instance that did not process the
revoking request. The design doc acknowledges the single-instance assumption and lists Redis as a follow-up; note that
Redis alone is not sufficient, because `LocalCacheExpiration` is also 30 s. That also needs a shorter local TTL or
tag-based eviction.

---

## 7. Registration, OTP and password recovery

### 7.1 `POST /auth/register` — and what it returns

`Features/Register/RegisterEndpoint.cs` → `RegisterRequestCommand` → `RegisterRequestCommandValidator` →
`RegisterService.RegisterAsync`. The validator is thorough: email format, plus `MinimumLength(6)`,
`MaximumLength(100)`, `Matches(@"[A-Z]")`, `Matches(@"[a-z]")`, `Matches(@"[0-9]")`, `Matches(@"[\W_]")` applied to
`x.register.PasswordHash`.

**That field name is a trap.** `RegisterRequestDTO`'s second positional parameter is `string PasswordHash`, and
`RegisterService` does `PasswordHash = _passwordHasherService.HashPassword(registerRequestDTO.PasswordHash)` — the
client sends the **plaintext password** in a field called `PasswordHash`. The validator's character-class rules only
make sense for plaintext, which confirms the intent, but anyone reading the DTO will assume the client pre-hashes.
`RegisterIntegrationTests` and `PasswordTokenIntegrationTests` both pass plaintext into it.

The re-registration guard checks the **OTP table, not the user table**:
`IsUserEmailExistInOtpVerificationAsync(registerRequestDTO.Email, true)`, a non-null result logging
`"Email already in use: {@Context}"` and throwing `Exception("Email already in use.")`. That repository method is
`.Where(ov => ov.Email == email && ov.IsUsed == isUsed).OrderByDescending(ov => ov.CreatedAt).FirstOrDefaultAsync()`, so
`isUsed: true` asks "has an OTP for this address ever been consumed". Two consequences:

- An address that exists in `Authusers` but has no *consumed* OTP row — an administrator-created account, a seeded
  account, or one registered before this table existed — **passes the guard** and produces a second pending
  registration. On verification, `SaveUserAsync` inserts a second `Authusers` row with the same email. There is no
  unique index to stop it (§1.1) and `GetUserDataAsync` resolves the ambiguity with an unordered `FirstOrDefaultAsync`.
- Repeated calls with an unconsumed row present insert a **new** `OtpVerification` row and email a new code each time.
  Nothing caps this; the endpoint is unauthenticated and unrated (§8.2).

The pending record is then built — **no `Authusers` row yet** — with
`PasswordHash = _passwordHasherService.HashPassword(registerRequestDTO.PasswordHash)`, `OtpCodeHash = HashOTP`,
`IsVerified = false`, `IsUsed = false`, `AttemptCount = 0`,
`ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes)`, persisted via `InsertOtpVerification(user)`. The response
is `user.Adapt<OtpVerificationResponse>()`, returned by the endpoint as `Results.Ok(response.otpVerificationResponse)`.
`DTO/OtpVerificationResponse.cs`:

```csharp
public record OtpVerificationResponse(
	long Id,
	Guid OtpId,
	string Email,
	string FirstName,
	string MiddleName,
	string LastName,
	string PasswordHash,
	string OtpCodeHash,
	bool IsVerified,
	bool IsUsed,
	int AttemptCount,
	DateTime CreatedAt,
	DateTime ExpiresAt,
	DateTime? VerifiedAt);
```

Mapster maps by name and both `PasswordHash` and `OtpCodeHash` exist on the source entity. **The 200 response to an
unauthenticated `POST /auth/register` contains the Argon2id password hash and the OTP hash.** See S1.
`RegisterIntegrationTests.Register_ShouldReturnOtp_WhenRequestIsValid` asserts only `.Email` and `.OtpId`, so the leak
is unpinned in both directions.

**Can a partially-registered user log in?** No, and the ordering guarantees it. The `Authusers` row is created at the
very end of `VerifyOtpAsync`, after the OTP is confirmed, copying `Email`, `PasswordHash`, `FirstName`, `LastName`,
`MiddleName` from the OTP record with a fresh `Guid.CreateVersion7()`. `IsApproved` is not set, so the column default
(`false`) applies, and `LoginWebAsync` rejects unapproved accounts before issuing anything. An abandoned registration
leaves an `OtpVerification` row holding an Argon2 hash and no login-capable user.

The residual risk is the **inverse** ordering problem: `IsUsed`/`IsVerified` are written and saved *before*
`SaveUserAsync`, and the two are not in a transaction. If `SaveUserAsync` throws, the OTP is consumed and the user does
not exist — the registrant must start over, and `IsUserEmailExistInOtpVerificationAsync(email, true)` will now report
"Email already in use" for an address that has no account.

### 7.2 `POST /auth/verify/otp` — the check order

`RegisterService.VerifyOtpAsync(email, otp)`. Plumbing `_logger.LogWarning` calls omitted; the gate order and every
state write are verbatim:

```csharp
		var existingOtpRecord = await _authRepository.IsUserEmailExistInOtpVerificationAsync(email, false);

		if (existingOtpRecord == null)
		{
			throw new Exception("No OTP record found for this email.");
		}

		var hashOtp = _hashService.Hash(otp);
		if (existingOtpRecord.AttemptCount >= MaxOtpAttempts)
		{
			existingOtpRecord.IsUsed = true;
			await _authRepository.UpdateVerificationCodeAsync(existingOtpRecord);
			throw new UnauthorizedAccessException("Too many invalid OTP attempts. Please request a new code.");
		}

		var isOtpValid = _hashService.Verify(hashOtp, existingOtpRecord.OtpCodeHash);

		if (!isOtpValid)
		{
			existingOtpRecord.AttemptCount += 1;
			if (existingOtpRecord.AttemptCount >= MaxOtpAttempts)
				existingOtpRecord.IsUsed = true;
			await _authRepository.UpdateVerificationCodeAsync(existingOtpRecord);
			throw new Exception("Invalid OTP.");
		}

		if (existingOtpRecord.IsUsed)
		{
			throw new Exception("OTP already used.");
		}

		if (DateTime.UtcNow > existingOtpRecord.ExpiresAt)
		{
			await this.ResendOtpAsync(existingOtpRecord);
			throw new InvalidOperationException("Your OTP has expired. A new code has been sent to your email.");
		}
```

Read in order:

1. **The record lookup filters `IsUsed == false`.** Once the cap consumes an OTP the record becomes invisible here and
   the caller gets "No OTP record found for this email." — a *different* message from "Invalid OTP.", which tells an
   attacker they hit the cap without being told so explicitly.
2. **The cap is checked before the hash comparison**, so a sixth attempt is rejected without consuming a guess and
   without leaking whether the sixth code was right.
3. **The `IsUsed` check is after the comparison and therefore largely dead** on this path — an `IsUsed == true` record
   is already excluded by step 1. It can only fire if the record was consumed between the two reads.
4. **The expiry check is last, and it auto-resends.** Submitting a *correct* code after expiry calls
   `ResendOtpAsync(existingOtpRecord)` — which resets `AttemptCount = 0` and `IsUsed = false` — then throws. So an
   expired-but-correct submission silently reopens the guessing budget. Submitting an *incorrect* code after expiry
   increments `AttemptCount` and never reaches the expiry branch, so the two paths behave differently for the same stale
   record.
5. On success `AttemptCount` is incremented **as well as** `IsVerified`/`IsUsed` being set, so a successful
   verification leaves `AttemptCount = 1`. Harmless, but `AttemptCount` is not a clean count of failures.

`MaxOtpAttempts` is `private const int MaxOtpAttempts = 5;` — a code constant, not configuration, unlike every other
threshold in this area.

**Is the input hashed before comparison everywhere an OTP is checked?** Yes. Auth's path is
`_hashService.Verify(hashOtp, existingOtpRecord.OtpCodeHash)` where `hashOtp = _hashService.Hash(otp)`. The only other
OTP-comparing code in the platform is
`ATS/Services/Settings/EmailAccountManagement/AtsEmailAccountManagementService.cs:302`,
`if (!_hashService.Verify(_hashService.Hash(request.OtpCode), otp.OtpCodeHash))` — same shape, same `HashService`.
`RegisterService.IsOtpSessionValidAsync` does **not** compare a code at all (it checks only `IsUsed`), so it is not a
verification path. Note the interface signature `bool Verify(string input, string hash)` names its first parameter
`input` while both call sites pass a *hash*; the implementation treats both arguments as base64url hashes so it works,
but the contract reads as though it would hash for you.

### 7.3 OTP resend — no throttle

`Features/ResendOTP/ResendOTPEndpoint.cs` maps `/verify/resend-otp` with **no `RequireAuthorization`** (gateway path
`/auth/resend-otp`, no `RateLimitPolicy`). `ResendOTPValidator` checks only that `userId` and `email` are present and
that the email parses. `ManualResendOtpCodeAsync` does three reads —
`IsUserEmailExistInOtpVerificationAsync(email, false)`, `OtpVerificationUserData(user)`, then
`IsUserEmailExistInOtpVerificationAsync` again on the same address — before calling `ResendOtpAsync`, whose first act is
`var otp = _otpService.GenerateOtp();` followed by `otpVerification.OtpCodeHash = _hashService.Hash(otp);
otpVerification.AttemptCount = 0; otpVerification.IsUsed = false;` and
`otpVerification.ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes);`, all persisted by
`UpdateVerificationCodeAsync` **before** the email is sent. A send failure therefore throws *after* the new hash is
stored, so a failed resend still rotates the code and invalidates the one already in the user's inbox. There is no
minimum interval and no per-address cap. Combined with §7.1's unbounded `InsertOtpVerification`, an unauthenticated
caller can drive arbitrary email volume to any address at 500 requests/second (S10).

### 7.4 Password recovery

Three endpoints, none authenticated: `forgot-password-email-send`, `is-change-password-token-valid`, `change-password`.

`ForgotPasswordService.ForgotPasswordAsync(email)` — the enumeration fix is real:

```csharp
		var user = await _authRepository.IsUserEmailExistAsync(email);

		if (user == null)
		{
			_logger.LogInformation("Password reset requested for an unknown email address.");
			return true;
		}
```

Both branches return `true` and the endpoint returns `Results.Ok(new SendForgotPasswordEmailResponse(result.IsEmailSent))`
either way. But the paths are not indistinguishable: for a known email a send failure logs
`"Failed to send password reset email to: {@Context}"` and throws `Exception("Failed to send password reset email.")` →
**500**, while an unknown email returns **200**, so an SMTP outage becomes an enumeration oracle for every address in
the outage window; the known path also performs token generation, a template render and an SMTP round trip that the
unknown path skips, an unmitigated timing difference; and `IsUserEmailExistAsync` filters `au.Email == email && au.IsActive`,
so a **deactivated** account gets the unknown-email response — a third distinguishable outcome for an address that does
exist.

Token generation and storage are correct: `_secureToken.GenerateSecureToken()` is hashed via
`_hashService.Hash(secureToken)`, and only the raw value goes into the emailed link,
`$"{_frontendBaseUrl}/reset-password?token={System.Net.WebUtility.UrlEncode(secureToken)}"`.
`SecureToken.GenerateSecureToken` is `RandomNumberGenerator.GetBytes(32)` rendered as base64url — 256 bits, and the only
places the raw value exists are the email body and that URL. The row stores `TokenHash = hashedToken`,
`ExpiresAt = UtcNow.AddMinutes(_passwordTokenExpiryMinutes)` (`Email:PasswordTokenExpirationInMinutes`),
`IsUsed = false`. Two ordering notes: the email is sent **before** the row is saved, so a save failure leaves a live link
that will never validate; and **prior tokens are not invalidated** by a new request, so N requests leave N simultaneously
valid tokens.

`ResetPasswordAsync(tokenHash, newPassword)` — the parameter is named `tokenHash` but holds the **raw** token, and the
first line hashes it: `var hashedToken = _hashService.Hash(tokenHash);`. Validation:

```csharp
	private async Task<PasswordResetTokenDTO> IsTokenValidInternal(string tokenHash)
	{
		var token = await _authRepository.GetUserTokenAsync(tokenHash);
		if (token == null || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
		{
			return new PasswordResetTokenDTO(Guid.Empty);
		}
		return new PasswordResetTokenDTO(token.UserId);
	}
```

with `GetUserTokenAsync` already filtering `prt.IsUsed == false`, so the `token.IsUsed` test inside is always false —
belt and braces, not a bug.

**Consumption is not atomic with validation.** The sequence is: validate → load user → hash new password →
`UpdateAuthUserPassword` → `GetUserTokenAsync(hashedToken)` *again* → set `IsUsed`/`UsedAt` →
`UpdatePasswordResetTokenAsUsedAsync`. Two concurrent requests carrying the same token both pass `IsTokenValidInternal`
and both write a password; whoever commits last owns the account. This is the same class of race
`RotateRefreshTokenAsync` was rewritten to close with a conditional `ExecuteUpdateAsync`; the reset path never got the
equivalent.

Then the session revocation the design doc describes — `RevokeAllSessionsAsync(isTokenValid.userId, "PasswordReset")`
followed by a per-id `_sessionValidator.InvalidateAsync(sessionId)`. The repository method loads the user's active rows,
sets `IsActive = false` / `RevokedAt` / `RevokedReason` on each, saves, and returns the ids. It is load-then-mutate
rather than one `ExecuteUpdateAsync`, so it is not safe against a login happening mid-reset — but the failure direction
is benign (a session created *after* the snapshot survives a reset meant to evict it). It revokes only
`AuthRefreshToken` rows: a §4 sid-less token survives a password reset.

`IsTokenValid(string tokenHash)` — the public one, behind `is-change-password-token-valid` — hashes and checks the same
three conditions and **does not consume**. It is a free, unauthenticated, unrated oracle for "is this reset token still
live". Harmless for a 256-bit token, but a stolen link can be probed without burning it.

### 7.5 `change-password`'s validator

`UpdatePasswordValidator` applies the same six password rules as registration, and the endpoint carries no
`RequireAuthorization` — correct, since the reset token *is* the credential. `Authusers` rows created by
`VerifyOtpAsync` therefore inherit a policy-checked password, and there is no other route that sets a password today.

---

## 8. Lockout, and the other entry points

### 8.1 Lockout — `LoginService` + `Data/Repository/Lockout/`

Two stores, and the split is the whole design. The **counter** is a HybridCache entry; the **lock** is an `AuthAttempts`
row. `GetAttempts(userid)` reads `$"{_userAttemptTag}_{userid}"` through
`_hybridCache.GetOrCreateAsync<string, int>(cacheKey, userid, async (userId, token) => 0, null, tags: [_userAttemptTag])`;
`SetAttempts(userid, attemptCount)` does `RemoveAsync(cacheKey)` then
`SetAsync(cacheKey, attemptCount, null, tags: [_userAttemptTag])`. `_userAttemptTag` is `"user_attempt"`, so the key is
`user_attempt_{userId}`. Both pass `null` for options → `DefaultEntryOptions` → **10-minute expiry,
`DisableDistributedCache`**. So:

- **The counter is per-user**, keyed on the resolved `Authusers.Id`. Never per-IP, and only reachable *after*
  `GetUserDataAsync` succeeds — an unknown username throws `NotFoundException` before any counting.
- **The counter never leaves the process.** N replicas give an attacker roughly N× the threshold before any one replica
  reaches it, and a rolling deploy zeroes every counter. Contrast `AuthAttempts`, which is durable.
- `SetAttempts` is remove-then-set, not an increment — two concurrent failures can both read 2 and both write 3, losing
  one.

`ErrorThreeAttempts` decides, and is called **twice per login** (before the password check with the cached count, and
after a failure with the incremented count). Abridged below: three dead assignments (`lockedUser = null;`, an unused
`var userId = lockedUserfromDB.UserId;`, and the discarded `IsSaved`/`IsDeleted` results) and the `_logger.LogWarning`
in the final branch are omitted; everything else is verbatim.

```csharp
		var lockedUserfromDB = await _authRepository.GetLockedUserAsync(UserID);

		// checking in cache if user is exist there(cache) so that we would not hit database prematurely
		if (currentAttempts == _maxFailedAttemptsBeforeLock && lockedUserfromDB is null)
		{
			bool IsSaved = await _authRepository.SaveLockedUserAsync(lockedUser);
			return true;
		}

		if (lockedUserfromDB is not null)
		{
			var attempts = lockedUserfromDB.Attempts;
			if (DateTime.UtcNow >= lockedUserfromDB.LockReleaseAt)
			{
				bool IsDeleted = await _authRepository.DeleteLockedUserAsync(lockedUserfromDB);
				return false;
			}

			// if users got 4 attempts and the time has not yet exceeded to lock time duration
			if (attempts == _maxFailedAttemptsBeforeLock)
			{
				return true;
			}
		}

		if (currentAttempts >= _maxFailedAttemptsBeforeLock)
		{
			return true;
		}

		return false;
```

- **Lock creation uses exact equality**: `currentAttempts == _maxFailedAttemptsBeforeLock`. If the lost-update race in
  `SetAttempts` skips the counter past the threshold — or the cache is evicted and repopulated higher — the durable
  `AuthAttempts` row is **never written**, and the only thing keeping the account locked is the in-memory
  `currentAttempts >= max` fallback, which dies with the 10-minute entry or the process. That last branch should be `>=`
  for the row write too.
- **Expiry is enforced by deletion**, not a flag: when `UtcNow >= LockReleaseAt` the row is deleted and the method returns
  `false`, so login proceeds to the password check. `DeleteLockedUserAsync` in
  `Data/Cache/Lockout/AuthCacheRepository.Lockout.Cache.cs` removes **both** `user_attempt_{userId}` and
  `userlockoutdate_{userId}` and evicts `LockedUsersTag`, so the counter cannot outlive the lock and re-lock the user on
  its own.
- **A locked account cannot log in even with the correct password**, because `ErrorThreeAttempts` runs before
  `VerifyPassword`. Intended, but it is what turns S6's per-user counter into a denial of service.

`GetLockedUserAsync` is cached for 10 minutes under `userlockoutdate_{userId}` and **caches null**, so a user with no lock
row costs no DB hit on later attempts; `SaveLockedUserAsync` evicts that key after writing. Thresholds are
`GetValue<int>("AuthWeb:MaxFailedAttemptsBeforeLockout")` and `GetValue<int>("AuthWeb:AccountLockDurationInMinutes")` —
both env-var placeholders in every appsettings, so the production values are not knowable from the repo. The unit-test
fixture uses `4` and `60`.

**Net effect (verified):** three or four requests to `/token/web/generatetoken` with a known email and any password lock
that account for `AccountLockDurationInMinutes`. No IP dimension, no allow-list, no CAPTCHA or backoff.
`/token/generatetoken` shares the counter but has no rate limit at all. An administrator can clear a lock via
`auth/deletelockeduser/{lockUserId}`, which is itself only `.RequireAuthorization()`.

### 8.2 Gateway routes and rate limits

`Modules/Auth/Path/AuthPaths.cs` declares ~35 routes; `SSO/Path/SSOPath.cs` five. Exactly **one** carries rate-limit
metadata. Everything else falls to the gateway's `default` bucket — `PermitLimit = 500`,
`Window = TimeSpan.FromSeconds(1)`, partitioned on `GatewayConstants.RateLimitPolicies.Default`.

| Gateway path | Backend | Rate limit | Endpoint auth |
|---|---|---|---|
| `/token/generatetoken` | `/login` | **default 500/s** | none |
| `/token/web/generatetoken` | `/loginweb` | `LoginPolicy` | none |
| `/token/web/getnewaccesstoken` | `/getnewaccesstoken` | default | none (cookie is the credential) |
| `/auth/register`, `/auth/verify/otp`, `/auth/validate/otp`, `/auth/resend-otp` | `/register`, `/verify/otp`, `/verify/validate/otp`, `/verify/resend-otp` | default | none |
| `auth/forgot-password-email-send`, `/auth/forgot-password/is-change-password-token-valid`, `/auth/forgot-password/change-password` | `/forgot-password-email-send`, `/is-change-password-token-valid`, `/change-password` | default | none |
| `/auth/logout` | `/logout` | default | none |
| `/auth/isAuthenticated` | `/isauthenticated` | default | `.RequireAuthorization()` |
| `/auth/getusers`, `/auth/edituser`, `/auth/addappsubrole`, … (13 admin slices) | same | default | `.RequireAuthorization()` |
| `sso/login/callback`, `sso/logout`, `Saml2`, `Saml2/Acs` | same | default | none |

`LoginPolicy` is `RateLimitPartition.GetFixedWindowLimiter(policyName, _ => new FixedWindowRateLimiterOptions { PermitLimit = 5,
Window = TimeSpan.FromSeconds(10), QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0 })`. The
partition key is `policyName` — the literal string `"LoginPolicy"`, identical for every request. So this is **one global
bucket of 5 web logins per 10 seconds for the entire platform**, not 5 per caller. The `AnonymousApplicationForm` arm
twenty lines below partitions on `httpContext.Connection.RemoteIpAddress?.ToString() ?? policyName`, with a comment
explaining exactly why a shared bucket is wrong for anonymous traffic. `LoginPolicy` guards anonymous traffic and does
not get that treatment (S5).

### 8.3 `isauthenticated`

The endpoint carries `.RequireAuthorization()` **and** calls `LoginService.IsAuthenticated()`, which returns `true` iff the
access and refresh cookies are both non-empty. Because authorisation runs first, a request reaching the handler already passed
JWT validation — so the `false` branch is reachable only when the access cookie is valid but the refresh cookie is missing. It
also blocks a thread (`sender.Send(...)` then `result.Result.Adapt<IsAuthenticatedResponse>()`): no `SynchronizationContext`
in ASP.NET Core, so it will not deadlock, but it parks a thread-pool thread per call on an endpoint the UI polls.

### 8.4 The SSO boundary — `Modules/SSO/`

`Features/LoginCallback/SSOLoginCallbackHandler.cs`. A grep for `GetAccessToken(` across the solution returns three
sites, all in Auth (§2.4) — **the SSO path never mints a platform JWT.** It calls `AuthenticateAsync(_signinScheme)` on
the temporary SAML cookie and throws `Exception("Authentication failed")` if that fails; extracts
`ClaimTypes.NameIdentifier`, `ClaimTypes.Email` and `ClaimTypes.Name` from the assertion; rebuilds the identity under the
cookie scheme (`new ClaimsIdentity(claimCollection, _signinScheme)`); and calls
`SignInAsync(_signinScheme, cookiePrincipal, …)` with `IsPersistent = true`,
`ExpiresUtc = UtcNow.AddDays(_cookieExpiryinDaysKey)`, `AllowRefresh = true`. It also writes a second cookie whose value
is the SAML `NameIdentifier`:

```csharp
		var emailCookieOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = _isHttps,
			SameSite = SameSiteMode.Lax,
			Domain = "cibi.com.ph", // for refactoring later use from config
			Path = "/", // for refactoring later use from config
			Expires = DateTimeOffset.UtcNow.AddDays(_cookieExpiryinDaysKey)
		};
```

Despite the config key `SSOMetadata:UserEmailCookieName`, the value written is `nameIdentifier`, not the email.
`Domain = "cibi.com.ph"` is hardcoded, so the cookie is sent to **every** subdomain — any sibling application on
`*.cibi.com.ph` receives the platform's SAML subject identifier, and any of them can overwrite or delete it.
`SSOLogoutHandler` deletes it with a matching hardcoded domain and calls `SignOutAsync(_signinScheme)` — but there is
nothing to revoke server-side, because nothing was recorded server-side.

| Control | Password login | SAML callback |
|---|---|---|
| Looks up `Authusers` | yes (`GetUserDataAsync`) | **no — never** |
| `IsApproved` gate | yes | **no** |
| Application/role grant gate | yes (`LoginWebAsync`) | **no** |
| Lockout / attempt counting | yes | **no** |
| `AuthRefreshToken` row | yes (web login) | **no** |
| `sid` → revocable | yes (web login) | **no** |
| Platform JWT | yes | **no** |

**So is it a weaker second door?** Not to the API, and this is worth stating precisely because it is the easy thing to get
wrong. `DefaultAuthenticateScheme` is JwtBearer, `AddAuthorization()` registers no policies, and all 111
`RequireAuthorization()` calls are parameterless — so the default policy authenticates against JwtBearer alone and **the
SAML cookie authenticates nothing protected** (§3.5). What the callback *does* produce is a `true` from
`sso/is-user-authenticated` (`IsUserAuthenticatedHandler` calls `AuthenticateAsync(_signinScheme)` and returns a bool) for
any subject the IdP asserts — including one with no `Authusers` row, no approval and no grants — plus an 8-hour sliding
cookie and the cross-subdomain identifier cookie above. Any future change that adds the sign-in scheme to a policy, or
mints a JWT from this callback, converts that into a full authentication bypass with none of the approval, grant or
lockout gates (S13).

The endpoint also computes a `safeReturnUrl` and never uses it: `var safeReturnUrl = string.IsNullOrEmpty(returnUrl) ? "/"
: returnUrl;` is passed into `SSOLoginCallbackCommand(safeReturnUrl)`, the handler ignores `request.ReturnUrl`, and the
endpoint returns `Results.Redirect(_blazorAppUrl)` from configuration. **No open redirect** — the discarded parameter is
dead code, not a hole. Worth knowing before someone "wires it up".

---

## 9. What is logged

Serilog is configured in `ServiceConfiguration.AddLoggingConfiguration`. The console sink is attached at the root
`MinimumLevel.Information()`, while the database sink is attached with an explicit floor:

```csharp
			loggerConfiguration.WriteTo.Sink(sink, LogEventLevel.Warning);
```

Two destinations, **different floors**: Information reaches stdout only; Warning and above are persisted to PostgreSQL
(`PostgreSqlBatchingSink`) and queryable through `PlatformLogging`'s `GetLogs`/`GetLogById`.

### 9.1 The MediatR pipeline logs every payload

`BuildingBlocks/Behaviors/LoggingBehavior.cs` is registered as an open behaviour by `AddAuthMediaTR`
(`config.AddOpenBehavior(typeof(LoggingBehavior<,>));`, alongside `ValidationBehavior<,>`), and it does this for
**every** command and query in the platform:

```csharp
			_logger.LogInformation("[START] Handling {Request} - Request Data: {@Request}", typeof(TRequest).Name, request);
			…
			_logger.LogInformation("[END] Handled {Request} - Response Data: {@Response}", typeof(TRequest).Name, response);
```

`{@Request}` destructures the whole object graph. There is no redaction, no `[Sensitive]` attribute, no allow-list. For
Auth:

| Command | Secret written to the log |
|---|---|
| `LoginCommand(username, password)` | plaintext password |
| `LoginWebCommand(LoginWebCred)` | plaintext password (`LoginWebCred.Password`) |
| `RegisterRequestCommand(RegisterRequestDTO)` | plaintext password (in the field named `PasswordHash`) |
| `UpdatePasswordCommand(UpdatePasswordRequestDTO(hashToken, newPassword))` | **the raw reset token and the new plaintext password** |
| `VerifyOtpCommand(OtpRequestDTO(Email, Otp))` | the raw OTP |
| `IsChangePasswordTokenValidCommand(ForgotPasswordTokenRequestDTO(userId, tokenHash))` | the raw reset token |
| `LoginResult` (response) | `LoginResponseDTO.access_token` — **the issued JWT**, for the §4 path |
| `OtpVerificationResponse` (response) | `PasswordHash` and `OtpCodeHash` |

Level is Information, so per the sink filter these land in **container stdout, not the PostgreSQL log table**. That is the
honest scope: not persisted-and-queryable in the platform's own log UI, but present in whatever collects stdout — Docker's
json-file driver, a log shipper, a crash dump, `docker logs`. Severity scales with that pipeline, which is not defined in
this repository.

This is what makes the design doc's checklist item *"Raw access/refresh/reset tokens never appear in application logs"*
false as written (**C9**), and it makes the password-recovery claim *"Token values were removed from structured log
context"* true but incomplete: they were removed from `ForgotPasswordService`'s hand-built `logContext` objects, while the
pipeline behaviour logs the whole command two frames up. The one Warning-level line in `LoggingBehavior` is safe —
`_logger.LogWarning("[PERFORMANCE] {Request} took {ElapsedSeconds} seconds", typeof(TRequest).Name, stopwatch.Elapsed.TotalSeconds);`
— type name and elapsed seconds, no payload.

### 9.2 What Auth's own services log at Warning+

These *do* reach the PostgreSQL log table. Every one puts the **email address** in a persisted row; none puts a password,
token or OTP value in one. `LoginService` logs `"Login failed: Invalid username or password for user: {@Context}"`,
`"Login failed: Invalid password for user. Attempt {Attempt}/{Max} {@Context}"`, `"Account is locked…"` and
`ErrorThreeAttempts`' `"Account temporarily locked…"` at Warning, each with `Email = username` plus `Action`/`Step`/
`Timestamp` and attempt counters. `RefreshTokenService` logs `"Refresh token cookie is missing {@Context}"`, `"Refresh
token is invalid or expired {@Context}"` and `"Failed to update refresh token for user: {@Context}"` with
`Action`/`Step`/`Timestamp` only — **no token, no user id**. `ForgotPasswordService` logs `"Failed to send password reset
email to: {@Context}"`, `"Failed to generate secure token for email: {@Context}"`, `"Failed to save password reset token
for email: {@Context}"` and `"Invalid or expired token for user: {@Context}"` — email only, **no token, no link**.
`RegisterService` logs `"Email already in use: {@Context}"`, `"Invalid OTP provided for email: {@Context}"`, `"OTP expired
for email: {@Context}"` and `"Failed to send OTP email to: {@Context}"` — email only, **no OTP, no hash**.

So the design doc's claim holds for the hand-written contexts and fails for the pipeline. Two secondary observations:
**failed-login email addresses accumulate in the platform log table**, one row per failure — a durable, queryable record of
every address anyone tried to log in as, useful for audit and also a ready-made enumeration dataset for anyone with
`PlatformLogging` read access. And `CustomExceptionHandler` logs every unhandled exception at Error
(`logger.LogError("Error Message: {exceptionMessage}, Time of occurrence {time}", exception.Message, DateTime.UtcNow);`);
no Auth exception message contains a secret — FluentValidation's messages are the rule texts, not the submitted values — so
that is a volume concern, not a leak. It does mean **every lockout and every unapproved-account rejection writes a
persisted Error row**, because of §9.3.

### 9.3 `UnauthorizedAccessException` is not mapped to 401

`BuildingBlocks/Exceptions/Handler/CustomExceptionHandler.cs` switches on the platform's own exception types —
`InternalServerException`, `ValidationException`, `BadRequestException`, `NotFoundException`, `UnauthorizedException`,
`ForbiddenException`, `ConflictException` — all in `BuildingBlocks.Exceptions`, globally imported by `Auth/GlobalUsing.cs`.
**`UnauthorizedAccessException` is the BCL type and is not in the list**, so it falls through to the `_ =>` arm, which sets
`context.Response.StatusCode = StatusCodes.Status500InternalServerError` and puts `exception.Message` in
`ProblemDetails.Detail` with `exception.GetType().Name` as the `Title`.

Every one of these throws therefore returns **HTTP 500** with `Title: "UnauthorizedAccessException"`: `"Too many failed
login attempts. Please try again later."` (both `LoginAsync` and `LoginWebAsync`, both call sites); `"Your account has not
been approved yet…"` (both); `"Your account has no assigned application…"` (`LoginWebAsync`); `"Invalid refresh token."`
and `"Refresh token was already used or is no longer active."` (`RefreshTokenService`); `"Invalid or expired token."`
(`ForgotPasswordService.ResetPasswordAsync`); `"Too many invalid OTP attempts. Please request a new code."`
(`RegisterService.VerifyOtpAsync`).

Three consequences. The design doc's *"Losing the atomic rotation returns `UnauthorizedAccessException`, allowing the client
to treat the session as unauthenticated"* does not hold at the HTTP layer — the client sees 500, and
`UI/FrontendWebassembly/Services/GeneralHandler/InterceptorHandler.cs` reacts to `HttpStatusCode.Unauthorized`
specifically, so a replay-lost refresh presents as a server error rather than triggering refresh-and-retry. Every one
writes a persisted Error row. And the distinct `Detail` strings are the enumeration surface in S7. By contrast
`NotFoundException` **is** mapped, so `"Invalid username or password."` returns a clean 404 — and
`RegisterService`/`VerifyOtpAsync`'s plain `throw new Exception(...)` calls also land in the 500 bucket.

---

## 10. Tests — what is pinned, and what is not

`Auth.UnitTests/` (15 files) and `Auth.IntegrationTests/` (13 files) under `Test/Test/BackendAPI/Modules/`. The
integration tests derive from `BaseIntegrationTest(IntegrationTestWebAppFactory factory)` and drive **`_sender.Send(command)`
directly** — MediatR, not HTTP. That single fact determines most of the gaps below.

### 10.1 Pinned

The four properties the design doc claims are all genuinely covered: **`sid`/`jti`** by
`JWTServiceTests.GetAccessToken_ShouldIncludeUniqueJtiAndProvidedSessionId` (asserts `sid == "42"` and that two tokens for
one session differ in `jti`); **atomic rotation** by
`RefreshTokenServiceTests.GetNewAccessTokenAsync_ShouldThrowUnauthorized_WhenAtomicRotationLosesRace`; **cache
invalidation** by the same file's `MockAuthSessionValidator.Verify(x => x.InvalidateAsync(1, …), Times.Once)`; and
**cookie-only responses** by `GetAccessTokenIntegrationTests.GetNewAccessToken_ShouldReturnOk_WhenRefreshTokenIsValid`
(`AccessToken.Should().BeEmpty("browser tokens are delivered only through HttpOnly cookies")`).

Beyond those: token validation under HS256 with `ClockSkew = Zero` and the `userId`/`email`/`fullName` claims;
`platformRoleId` de-duplication and positive filtering (`[1,2,1]` → `"1","2"`) plus `atsRoleId`/`atsClientId`; Argon2id
salting and verification (`PasswordHasherServiceTests` ×3); `HashToken` determinism; missing/invalid refresh cookie;
lockout progression, already-locked, expired-lock and attempt-clearing (`LoginServiceTests` ×4, `LoginIntegrationTests` ×5);
thirteen OTP paths (`RegisterServiceTests`); four reset-token paths (`PasswordTokenIntegrationTests`); three logout paths
(`LogoutIntegrationTests`); `ICurrentUser` claim parsing, malformed-claim rejection and missing `HttpContext`
(`CurrentUserTests` ×3).

`CurrentUserTests.CurrentUser_ShouldReturnNull_ForMissingOrMalformedClaims` deserves a note: its `ClaimsIdentity` is built
**without an authentication type**, so `IsAuthenticated` is false while `PlatformRoleIds` still returns `[10, 11, 21]`. That
is the exact shape `AtsAuditService` guards against with `_currentUser.IsAuthenticated && _currentUser.IsPlatformSuperAdmin`
— `IsPlatformSuperAdmin` alone is not a safe gate, and the test documents why.

### 10.2 Not tested — the security properties with no coverage

| Untested property | Why it matters |
|---|---|
| **`AuthSessionValidator` itself.** No test file exists; the only reference in `Test/` is a `Mock<IAuthSessionValidator>` | The `UserId == userId` binding, the per-request `ExpiresAt` evaluation, the 30 s window and the cache-miss fallback are all unverified. A refactor moving the predicate inside the factory would pass every existing test |
| **`OnTokenValidated` / `OnMessageReceived` / `AddJwtAuthentication`.** No test constructs the JWT pipeline | Cookie-vs-bearer precedence, the sid-less early return and `context.Fail` are entirely unpinned — including C1 and §4 |
| **Cookie flags.** `HttpOnly`, `Secure`, `SameSite`, `Path`, `Expires` on all four setter copies | Nothing would fail if one drifted |
| **`LogoutAsync` actually revoking** (§10.3) | The named test never reaches the revocation branch |
| `RevokeAllSessionsAsync` after a password reset | The design doc's headline "reset evicts stolen sessions" claim has no test |
| Reset-token single-use under concurrency | §7.4's race |
| `OtpVerificationResponse` contents | §7.1's leak is invisible to the suite |
| `LoggingBehavior` redaction | §9.1 |
| Registration duplicate-email / missing `Authusers` check; resend throttling; OTP-cap renewability | §7.1, §7.3 |
| Any HTTP status code for the `UnauthorizedAccessException` paths | §9.3 — integration tests assert the exception type, never the response |
| SSO: callback, cookie domain, `IsUserAuthenticated` | `Modules/SSO` has **no test project at all** |

### 10.3 Two tests that pass for the wrong reason

**`LogoutIntegrationTests` never reaches the revocation code.** Its helper seeds `var hashed = ComputeSha256Base64(refreshToken);`
using a private helper built on `SHA256.Create()`. `RefreshTokenService.HashToken` is **SHA-512** (§5.5), so
`FindActiveRefreshTokenByHashAsync` never matches, `session is not null` is never true, and `UpdateRevokeReasonAsync` /
`InvalidateAsync` are never called. `LogoutAsync` still returns `true` from the `finally` path and
`result.IsLoggedOut.Should().BeTrue()` passes. All three logout tests assert only that boolean; none asserts
`IsActive == false`, `RevokedReason == "UserLogout"`, or that the cookies were deleted. The sibling file gets it right —
`GetAccessTokenIntegrationTests.ComputeSha512Base64` — so the two files disagree about the storage format and only one
matches production. Both are hand-copied duplicates of `HashToken`; calling the real method would have made the mismatch
impossible.

**`PasswordTokenIntegrationTests.UpdatePassword_ShouldThrowUnauthorized_WhenTokenExpired`** seeds `TokenHash = tokenHash`
— the **raw** GUID string, unhashed — and submits the same raw string. `ResetPasswordAsync` hashes the submission, so
`GetUserTokenAsync` finds nothing and the assertion
`ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid or expired token.")` passes on a hash mismatch, never
reaching the `ExpiresAt < DateTime.UtcNow` branch it is named for. The two tests that do hash correctly
(`_hashService.Hash(rawToken)`) are the valid ones.

### 10.4 Known-broken build

The design doc's verification note records that combined `Test.csproj` compilation was blocked by unrelated ATS
`DisputeOrderService` tests omitting an `IOrderHistoryService` constructor argument. I did not run the suite for this
document, so treat the inventory above as **read from the test sources, not from a green run**.

---

## 11. Security findings, ranked

Each states the precondition, the action and the impact, then points at the trace that establishes it. **Verified** means I
read the code path end to end; **plausible** means the mechanism is in the code but I could not demonstrate the outcome from
this repository.

### S1 — `POST /register` returns the OTP hash and the password hash — **verified, Critical**

*Precondition:* none; the endpoint is unauthenticated and unrated. *Action:* register any address, read the 200 body.
*Impact:* `OtpVerificationResponse.OtpCodeHash` is `HashService.Hash(otp)` — **unsalted SHA-512** of a six-digit code.
Hashing 10⁶ candidates takes a fraction of a second, so the caller recovers the OTP without access to the mailbox and
completes `VerifyOtpAsync` at will. Email verification for self-registration is bypassed. `PasswordHash` in the same body
is the Argon2id verifier for the password the caller just submitted — less severe (they know the password) but it puts a
crackable hash in every proxy, browser and log that touches the response. Evidence: §7.1.

### S2 — The 13 user-management command slices check authentication only — **verified, Critical**

*Precondition:* any valid platform JWT — including one from `/token/generatetoken`, or one held by the lowest-privilege
approved user. *Action:* `POST /auth/addappsubrole` with `{ UserId: <self>, AppId: <any>, SubMenuId: <any>, RoleId: 1,
AssignedBy: <anyone> }`; or `PATCH /auth/edituser` with `{ Email: <pending account>, IsApproved: true }`. *Impact:* full
privilege escalation to `PlatformRoleIds.SuperAdmin` (id `1`), self-approval of pending accounts, arbitrary grant/revoke
over every other user, and a forgeable `AssignedBy` attribution field.

All 13 command endpoints end in a **parameterless** `.RequireAuthorization()`; `AddAppSubRoleHandler` passes the request
straight to `AppSubRoleService.AddAppSubRoleAsync`, which never touches `ICurrentUser`; `UserService.EditUserAsync` does
`existingUser.IsApproved = userDTO.IsApproved;` from the request. `AddAppSubRoleCommandValidator` checks that the ids are
non-empty and positive — **shape, not authority**. The platform *has* the right primitive and uses it in ATS
(`AtsAccessScopeResolver`, `AtsAuditService`, `AtsAssistantPlugin`, `GetMyAccessHandler` all read
`ICurrentUser.IsPlatformSuperAdmin`); **no Auth user-management service reads `ICurrentUser` at all**. That the logic is
concentrated in ATS is what makes this easy to miss — Auth administers the roles ATS then enforces. Evidence: §8.2, §3.5.

### S3 — `/token/generatetoken` issues an unrevocable, body-delivered JWT with no rate limit — **verified, High**

*Precondition:* valid credentials for any account. *Action:* `POST /token/generatetoken`, keep the `access_token`.
*Impact:* no `sid`, so `OnTokenValidated` returns at its first line; no `AuthRefreshToken` row, so neither logout nor
`RevokeAllSessionsAsync` can reach it. It stays valid until `Jwt:ExpiryInMinutes` elapses regardless of password reset,
logout or role removal, and it is JavaScript-readable wherever the call is made from a browser. The route carries no
`RateLimitPolicy`, so it runs at 500 req/s while the browser login beside it runs at 5 per 10 s. Evidence: §4, §8.2.

### S4 — Plaintext passwords, OTPs, reset tokens and JWTs are written to the log stream — **verified, High**

*Precondition:* any request to any Auth command endpoint. *Action:* read container stdout. *Impact:* `LoggingBehavior` logs
`{@Request}` and `{@Response}` at Information with no redaction, capturing plaintext passwords (three commands), the raw
reset token **and** new password (`UpdatePasswordCommand`), the raw OTP (`VerifyOtpCommand`), the issued JWT
(`LoginResult.access_token`) and `OtpVerificationResponse`'s hashes. Scope is bounded: the PostgreSQL sink is filtered to
`LogEventLevel.Warning`, so these do **not** reach the queryable platform log table — they reach stdout and whatever collects
it. That still contradicts the design doc's checklist item outright. Evidence: §9.1.

### S5 — `LoginPolicy` is one global bucket for the whole platform — **verified, High**

*Precondition:* none. *Action:* send 5 requests per 10 seconds to `/token/web/generatetoken` from one host. *Impact:* every
other user's web login gets **429** for the rest of the window. The partition key is the policy-name literal, not the client
IP, while the sibling `AnonymousApplicationForm` arm partitions on `RemoteIpAddress` with a comment explaining exactly why a
shared bucket is wrong for anonymous callers. Evidence: §8.2.

### S6 — Lockout is per-user only, per-instance only, and its durable row can be skipped — **verified, High**

*Precondition:* a known email address. *Action:* three or four failed logins. *Impact:* the victim is locked out for
`AccountLockDurationInMinutes` and cannot authenticate **even with the correct password**, because `ErrorThreeAttempts` runs
before `VerifyPassword`. No IP dimension and no allow-list, so this is a one-request-per-victim denial of service against any
enumerated address — and S7 supplies the enumeration. Compounding it: the counter lives in a `DisableDistributedCache`
HybridCache entry, so it is lost on restart and multiplied by replica count; and the durable `AuthAttempts` row is written
only on exact equality with the threshold, so the lost-update race in `SetAttempts` can skip it entirely and leave the lock
held only by a 10-minute cache entry. Evidence: §8.1.

### S7 — Account and state enumeration through distinct messages and status codes — **verified, Medium-High**

*Precondition:* none. *Action:* submit credentials and compare responses. *Impact:*

| Condition | Status | Detail |
|---|---|---|
| Unknown email / known email, wrong password | **404** | "Invalid username or password." — correctly identical |
| Known email, locked | **500** | "Too many failed login attempts. Please try again later." |
| Known email, valid password, unapproved | **500** | "Your account has not been approved yet…" |
| Known email, valid password, no grants | **500** | "Your account has no assigned application…" |
| Address with a consumed OTP row | **500** | "Email already in use." (`POST /register`) |
| OTP cap reached / wrong / no record | **500** | "Too many invalid OTP attempts…" / "Invalid OTP." / "No OTP record found for this email." |

The lockout row is reachable *without* a valid password: three failures against an existing address produce it, while a
non-existent address keeps returning 404 forever. That is a password-free enumeration oracle. The "not approved" and "no
assigned application" rows require a valid password and so leak account state rather than existence. Forgot-password is the
one flow the design doc claims to have equalised and is very nearly equal — but a send failure throws for a known email only,
and the known path does an SMTP round trip the unknown path skips. Evidence: §7.4, §8.1, §9.3.

### S8 — Every authentication failure returns 500 — **verified, Medium**

`UnauthorizedAccessException` is not in `CustomExceptionHandler`'s switch, which lists the platform's own
`UnauthorizedException`. Lockouts, unapproved accounts, refresh-token replays and expired reset tokens therefore all return
**500** instead of 401, each writing a persisted Error row. The design doc's claim that a lost rotation *"returns
`UnauthorizedAccessException`, allowing the client to treat the session as unauthenticated"* does not hold at the HTTP layer,
and `InterceptorHandler` keys its refresh-and-retry on `HttpStatusCode.Unauthorized` specifically. It also inflates the
500-rate metric the HighErrorRate alert watches. Evidence: §9.3.

### S9 — Web sessions slide indefinitely, and remember-me can be inverted — **verified, Medium**

`RotateRefreshTokenAsync` renews `ExpiresAt` to `UtcNow.AddDays(60)` on every refresh, so a session used at least once per 60
days never expires. Neither `AuthWeb:HttpCookieOnlyRefreshTokenInDays` nor `AuthWeb:AuthWebHttpCookieOnlyDays` appears in any
`appsettings.*.json`, so both resolve to 60 everywhere; remember-me reads a *third* key,
`AuthWeb:CookieExpiryInDayIsRememberMe` — `7` in the test fixture. If production configures it below 60, **ticking "remember
me" shortens the session**. Two services reading different keys for one nominal setting can also drift independently.
Evidence: §5.1, §5.3.

### S10 — OTP resend is unauthenticated, unthrottled and resets the attempt cap — **verified, Medium**

`POST /auth/resend-otp` requires nothing but an existing unconsumed OTP row, and `ResendOtpAsync` sets `AttemptCount = 0;
IsUsed = false` and mints a fresh code — so the five-attempt budget is renewable without limit: 5 guesses per resend, one HTTP
call per resend, no interval, no cap, default 500 req/s. `RegisterAsync` is likewise unbounded, its guard checking only for a
*consumed* row, so each call inserts another `OtpVerification` and sends another email — arbitrary email volume to any
address from an unauthenticated caller. Secondary: the counter reset is persisted **before** the email is sent, so a send
failure still rotates the code and invalidates the one already in the user's inbox. Evidence: §7.1–§7.3.

### S11 — No unique constraint on `Authusers.Email`, and no check against `Authusers` at registration — **verified, Medium**

`RegisterAsync` guards on `OtpVerification.IsUsed`, never on `Authusers`; the only index on that table is
`(LastName, FirstName, Id)`; and `GetUserDataAsync` / `IsUserEmailExistAsync` both resolve with an unordered
`FirstOrDefaultAsync`. So a duplicate-email account can be created — by re-registering an admin-created or seeded address, or
by the race in S18 — and which row authenticates afterwards is **nondeterministic**. Password resets target
`GetRawUserAsync(isTokenValid.userId)` by id, so a reset applied to the wrong row leaves the other usable. Evidence: §1.1,
§1.4, §7.1.

### S12 — Session revocation does not cross instances — **verified, Medium**

`InvalidateAsync` is `_cache.RemoveAsync(key)` on the local instance, so with more than one replica a revocation on instance A
leaves instance B answering "active" from its own L1 entry for up to **30 seconds**. `SessionTag = "auth-sessions"` is
registered on every entry but **no code calls `RemoveByTagAsync` with it**, so there is no broadcast path either. The design doc
acknowledges the single-instance assumption; note that Redis alone would not fix it, because `LocalCacheExpiration` is also
30 s. Live impact today is nil — but this is the assumption that breaks first under horizontal scale. Evidence: §6.3.

### S13 — SAML assertion is trusted without consulting the user table — **verified, Medium (bounded)**

`SSOLoginCallbackHandler` never queries `Authusers`: no existence, `IsApproved`, grant or lockout check, no
`AuthRefreshToken` row, no platform JWT. It signs in any subject the IdP asserts and writes a second cookie containing the
SAML `NameIdentifier` with a hardcoded `Domain = "cibi.com.ph"`, so every subdomain receives it and any of them can overwrite
or delete it. `AllowUnsolicitedAuthnResponse = true` removes the request/response binding.

**The impact is bounded, and the bound is the important part:** the SAML cookie authenticates **no protected endpoint**,
because the default scheme is JwtBearer, no policy is registered, and all 111 `RequireAuthorization()` calls are
parameterless. What it grants is a `true` from `sso/is-user-authenticated` for a subject that may not exist in this platform,
plus that cross-subdomain cookie. Any future change adding the sign-in scheme to a policy, or minting a JWT from the callback,
converts this into a full authentication bypass with none of the approval, grant or lockout gates. *Plausible, not
demonstrated:* exploitability of the unsolicited-response setting depends on IdP-side binding I could not inspect. Evidence:
§3.5, §3.6, §8.4.

### S14 — Cookie `Secure` is configuration-driven while forwarded headers are untrusted — **verified, Low-Medium**

All four JWT/refresh cookie setters use `Secure = _isHttps` from `AuthWeb:isHttps`, while `Program.cs` clears both
`KnownNetworks` and `KnownProxies` — so no proxy is trusted, `X-Forwarded-Proto` is ignored, `Request.IsHttps` stays false
behind the gateway, and `app.UseHttpsRedirection()` cannot determine an HTTPS port and effectively no-ops. Deriving `Secure`
from configuration rather than the request is therefore deliberate, but it puts the flag one mis-set environment variable away
from shipping session cookies over plain HTTP; `AUTHWEB__ISHTTPS` is a `${…}` placeholder in every appsettings, so the deployed
value is not knowable from the repo. The Saml2 sign-in cookie is unaffected — it hardcodes `CookieSecurePolicy.Always`.
Evidence: §3.5, §5.2.

### S15 — Cryptographic hygiene — **verified, Low**

- **Two SHA-512 helpers, two encodings.** `HashService.Hash` emits base64**url**
  (`.Replace('+','-').Replace('/','_').TrimEnd('=')`); `RefreshTokenService.HashToken` emits standard base64. Both unsalted, and
  a value stored through one will never match a lookup through the other. Unsalted SHA-512 is the *right* choice for
  `SecureToken` and `GenerateRefreshToken` (256- and 512-bit CSPRNG values, nothing to precompute against) and the *wrong* one
  for `OtpService.GenerateOtp()`'s six decimal digits — a million-candidate dictionary is trivial, which is what makes S1 fatal
  rather than merely untidy. §10.3's broken logout test is the same two-alphabet mistake made with SHA-256.
- **Modulo bias in OTP generation:** `otp += (tokenData[i] % 10).ToString();`. 256 is not a multiple of 10, so digits 0–5 occur
  with probability 26/256 and 6–9 with 25/256 — entropy drops from ~19.93 to ~19.90 bits. Immaterial next to S1 and S10, but a
  real bias and one line to fix.
- **`FixedTimeEquals` is used consistently where a secret is compared** — and the places that do not use it are correct not to.
  `PasswordHasherService.VerifyPassword` compares Argon2 output with `CryptographicOperations.FixedTimeEquals(storedHash,
  computedHash)`; `HashService.Verify` compares base64url-decoded hashes with `FixedTimeEquals`, its comment explaining the
  length-mismatch case; `RefreshTokenService.ValidateHashToken` does the same (no production caller). The refresh-token and
  reset-token lookups instead compare **hashes by SQL equality**, safe because their preimages are high-entropy random; the one
  short secret in the platform, the OTP, goes through `HashService.Verify` and therefore through `FixedTimeEquals`. To answer the
  question posed by the employment-verification module's unused `IHashService.Verify`: **Auth does compare secrets in constant
  time**, via that same shared implementation.
- **Argon2id parameters are within OWASP guidance but unversioned.** `MemorySize = 1024 * 64` (64 MiB), `Iterations = 4`,
  `DegreeOfParallelism = 1`, `SaltSize = 16`, `HashSize = 32`, salt from `RandomNumberGenerator.GetBytes(SaltSize)`, stored as
  `$"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}"` — **no algorithm or parameter marker**. `VerifyPassword`
  hardcodes the same four constants and returns `false` if `parts.Length != 2`, so raising any parameter silently invalidates
  every stored hash with no migration path and no way to distinguish "wrong password" from "old parameters".
- **`AesGcmSecretProtector` is not used by Auth at all.** It is registered once —
  `services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();` in `ATS/ServiceConfig/ATSServiceConfiguration.cs` — for ATS
  email-account SMTP credentials, the one secret here that must be *recovered* rather than *compared*. Auth's secrets are all
  one-way, so hashing is the correct primitive throughout. Worth recording because the two are easy to conflate:
  `Security:SecretProtectionKey` protects nothing in Auth, and an Auth secret must never be routed through `ISecretProtector`.

### S16 — Unindexed lookups on unauthenticated endpoints — **verified, Low**

`PasswordResetToken.TokenHash`, `OtpVerification.Email` and `Authusers.Email` have no index (§1.4) and are each reached by an
unauthenticated endpoint at the gateway's 500 req/s default. Sequential scans that grow with table size, drivable by an
anonymous caller — a cheap load amplifier, worsening as `OtpVerification` accumulates the rows §7.1 never caps.

### S17 — Dead and unwired security surface — **verified, Low**

`RefreshTokenService.RevokeTokenAsync()` is `throw new NotImplementedException();` on the public `IRefreshTokenService`
contract; `ValidateHashToken` has no production caller; `LogoutDTO` and `LogoutCommand.LegacyRequest` survive but are ignored;
`GetNewUserDataAsync` selects `authRefreshToken.TokenHash` into `UserDataDTO.refreshToken`, which `userData.Adapt<LoginDTO>()`
then drops; `SSOLoginCallbackCommand.ReturnUrl` is computed, sanitised and never read; `IsAuthenticatedEndpoint` blocks on
`.Result` and, because it requires authorisation, can never return the `false` its service computes. None is exploitable; each
is a place where a future reader will assume a control exists.

### S18 — Concurrent OTP verification can create duplicate users — **plausible, not demonstrated**

`VerifyOtpAsync` reads the record, verifies, writes `IsUsed`/`IsVerified`, then inserts the `Authusers` row — with no
transaction and no unique index on `Email`. Two simultaneous submissions of the same correct OTP should both pass. I did not
execute this; the mechanism is fully in the code, and S11 records the consequence that *is* verified.

---

## 12. Wiring — what is registered where

### 12.1 `Modules/Auth/ServiceConfig/AuthServiceConfiguration.cs`

`AddAuthServices` registers `AddHttpContextAccessor`, then `ICurrentUser`→`CurrentUser`,
`IPasswordHasherService`→`PasswordHasherService` (**transient** — the odd one out, harmless because it is stateless),
`IJWTService`, `IAuthRepository`→`AuthRepository`, `IRefreshTokenService`, `IAuthSessionValidator`, `ILoginService`,
`AddKeyedScoped<IEmailService, EmailService>("auth")`, `IOtpService`, `IHashService`→`HashService`, `IRegisterService`,
`IForgotPasswordService`, `ISecureToken`→`SecureToken`, and the eight user-management/profile services plus `IAuthQueries`
— all scoped except the two transient ones and `AuthInitialData`. Then one decorator and a wall of forwarding, all of the
form `services.AddScoped<IFooRepository>(provider => provider.GetRequiredService<IAuthRepository>());`:

```csharp
		services.Decorate<IAuthRepository, AuthCacheRepository>();
```

covering all twelve focused contracts (`IApplicationRepository`, `IAppSubRoleRepository`, `ILockoutRepository`,
`ILoginRepository`, `IPasswordRecoveryRepository`, `IRefreshTokenRepository`, `IRegistrationRepository`,
`IRoleRepository`, `ISubMenuRepository`, `IUserDirectoryRepository`, `IUserProfileRepository`, `IUserRepository`). This is
the aggregate-forwarding shape ATS uses. **Every Auth read therefore passes through `AuthCacheRepository`** — which is why
§6.2 had to check the decorator before concluding that `GetSessionAsync` is uncached. Add a new focused contract and it
must be forwarded here or it will not resolve. `AuthRepository` and `AuthCacheRepository` are partial types; each business
folder owns a focused contract plus matching partial files under `Data/Repository/<Area>` and `Data/Cache/<Area>`.

`IEmailService` is **keyed** `"auth"`; every Auth consumer injects `[FromKeyedServices("auth")] IEmailService`. ATS registers
its own key. Omitting the attribute is a runtime resolution failure, not a compile error. `AddAuthMediaTR` is where the two open
behaviours are attached, and therefore where §9.1's logging is enabled for Auth:
`config.AddOpenBehavior(typeof(ValidationBehavior<,>));` and `config.AddOpenBehavior(typeof(LoggingBehavior<,>));`.
`AddAuthInfrastructure` registers `AuthApplicationDbContext` with `UseNpgsql` on `OnePlatform_Connection` and
`MigrationsAssembly("APIs")` — migrations live in the API project, not the module.

### 12.2 Composition root — `BackendAPI/API/APIs/Program.cs`

`AddLoggingConfiguration` → `AddModuleMediaTR` → `AddModuleCarter` → `AddHybridCaches` → `AddModuleServices` →
`AddJwtAuthentication` → `AddModuleInfrastructure` → AI/OSS/observability. `AddModuleServices` runs **before**
`AddJwtAuthentication`, so `IAuthSessionValidator` is registered before `OnTokenValidated` could resolve it — irrelevant at
runtime (resolution is deferred to `RequestServices`) but the ordering is what makes `AddModuleCarter`'s assembly list and
`AddModuleServices` the two places a new module must appear.

`AppConfiguration.UseCustomMiddlewares` gives the pipeline order: `UseHttpsRedirection` → `UseRouting` → `UseHttpMetrics` →
`UseExceptionHandler` → `UseCors("CorsPolicy")` → `UseAuthentication` → `UseAuthorization` → `MapControllers` → `MapCarter`.
`UseExceptionHandler` sits *inside* `UseHttpMetrics`, and the file comments why — an exception must be counted as a 500, not a
200. Consequence here: `CustomExceptionHandler` is what turns §9.3's exceptions into `ProblemDetails`, after metrics have
already recorded the status.

### 12.3 Strings that must agree across files, with nothing enforcing it

| Value | Places it appears independently |
|---|---|
| Access-cookie name | `HttpCookieOnlyKey` read in `ServiceConfiguration.AddJwtAuthentication`, `LoginService`, `RefreshTokenService` |
| Refresh-cookie name | `AuthWeb:AuthWebHttpCookieOnlyKey` read in `LoginService`, `RefreshTokenService` |
| Session cache key format | `$"auth-session:{sessionId}"` in **both** `IsActiveAsync` and `InvalidateAsync` |
| Attempt-counter key | `$"{_userAttemptTag}_{userid}"` in `LoginService` **and** `$"{_userAttemptTag}_{lockedUser.UserId}"` in `AuthCacheRepository.DeleteLockedUserAsync` — two classes, two `_userAttemptTag` fields, both `"user_attempt"` |
| Lockout-cache key | `$"{UserLockoutDate}_{userId}"` (`"userlockoutdate"`) in `AuthCacheRepository`, read and removed in three methods |
| Refresh-token hash format | `RefreshTokenService.HashToken` (SHA-512/base64) **and** `GetAccessTokenIntegrationTests.ComputeSha512Base64` **and** `LogoutIntegrationTests.ComputeSha256Base64` — the third is wrong (§10.3) |
| Revocation reasons | `"UserLogout"` (logout), `"PasswordReset"` (reset) — bare literals, no shared constant, no column constraint |
| Carter route ↔ gateway `PathSet` | e.g. `app.MapPost("loginweb")` ↔ `{ "PathSet", "/loginweb" }`; `/verify/resend-otp` ↔ `/auth/resend-otp` |
| `AuthWeb:isHttps` casing | `"AuthWeb:isHttps"` in `LoginService`/`RefreshTokenService`, `"AuthWeb:IsHttps"` in `SSOLoginCallbackHandler`, `"IsHttps"` in appsettings — .NET config keys are case-insensitive, so all three resolve |
| Non-remember-me refresh lifetime | `AuthWeb:AuthWebHttpCookieOnlyDays` (`LoginService`) vs `AuthWeb:HttpCookieOnlyRefreshTokenInDays` (`RefreshTokenService`) — **two keys for one setting**, neither in appsettings (S9) |
| Saml2 cookie domain | `"cibi.com.ph"` hardcoded in `SSOLoginCallbackHandler` **and** `SSOLogoutHandler`, with a `// for refactoring later use from config` comment on the first |

### 12.4 Project references

`Modules/Auth/Auth.csproj` is referenced by `Modules/ATS/ATS.csproj` (alongside `BuildingBlocks` and `OMS`) — ATS consumes
`IAuthQueries`, `ICurrentUser`, `AuthClaimTypes` and `PlatformRoleIds`. `SSO` references neither Auth nor ATS; it reaches the
shared cookie scheme only through `SSOMetadata:SigninScheme`, read from configuration in four places (`ServiceConfiguration`,
`SSOLoginCallbackHandler`, `SSOLogoutHandler`, `IsUserAuthenticatedHandler`). **The SSO ↔ Auth coupling is a configuration
string, not a project reference** — which is exactly why §8.4's gaps are invisible to the compiler.

---

## 13. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| `JWTService.GetClaims` — add, rename or drop a claim | `CurrentUser.GetClaimValue`'s argument order, `OnTokenValidated`'s `FindFirst(ClaimTypes.NameIdentifier)`, `LoggingBehavior`'s `FindFirst("userId")` | Four readers of the same claim names in three assemblies, none compile-time bound (§2.5, §3.4, §9.1) |
| `GetAccessToken`'s `sessionId` parameter or the `is > 0` guard | `LoginService.LoginAsync` (passes nothing), `LoginWebAsync`, `RefreshTokenService` | Dropping `sid` silently disables session liveness for that token — `OnTokenValidated` returns early (§2.3, §3.4) |
| `Jwt:ExpiryInMinutes` | The access-cookie `Expires` in **four** setters, and `RefreshTokenService.ExpireInMinutes()` | The cookie lifetime derives from the same value; a mismatch leaves a cookie outliving a rejected token (§3.1, §5.2) |
| `ClockSkew` | `JWTServiceTests.GetAccessToken_ShouldReturnValidJwt_WithExpectedClaims`, which hardcodes `TimeSpan.Zero` in its own `TokenValidationParameters` | The test builds its own validator rather than reusing the host's, so it will not track the change (§10.1) |
| `OnMessageReceived` | `ats-public-api_code_explanation.md` §9.4 | That section's ordering is wrong and its proposed fix is inert (§3.3) |
| `OnTokenValidated` | That **no test covers it** | There is no HTTP-level Auth test at all; every integration test calls `_sender.Send` (§10) |
| `AuthSessionValidator`'s `HybridCacheEntryOptions` | `AddHybridCaches`' `DefaultEntryOptions.Flags` | Locality depends on per-property defaulting of `DisableDistributedCache`; restating the options object can silently re-enable the Redis L2 (§6.1, **C2**) |
| The `auth-session:{sessionId}` key format, or `SessionTag` | `InvalidateAsync` in the **same file** | Two independent interpolations; a mismatch means revocation never evicts. And no `RemoveByTagAsync("auth-sessions")` exists (**C13**, §6.3) |
| `RefreshTokenService.HashToken` | `FindActiveRefreshTokenByHashAsync`, `LogoutAsync`, `RotateRefreshTokenAsync`, both test helpers | Stored hashes are not versioned; changing the algorithm orphans every live session. `LogoutIntegrationTests` already disagrees with production (§5.5, §10.3) |
| `RotateRefreshTokenAsync`'s `SetProperty` list | `AuthSessionValidator.IsActiveAsync`'s use of `ExpiresAt`, and any future audit query on `CreatedAt` | Rotation overwrites `CreatedAt` and renews `ExpiresAt` — the row is not an immutable session record (**C8**, §5.3) |
| `_httpCookieOnlyRefreshTokenInDays` or `_cookieExpiryinDaysKey` | The **other** service's key: `AuthWeb:AuthWebHttpCookieOnlyDays` vs `AuthWeb:HttpCookieOnlyRefreshTokenInDays` | Two keys for one setting, read by two services, neither in appsettings (S9) |
| Cookie flags in any one of the four setters | The other three, plus `RemoveAccessAndRefreshTokenCookie` | `Delete` without matching `Path`/`SameSite` will not remove the cookie; the four copies are hand-synced (§5.2) |
| `AuthWeb:isHttps` | `Program.cs`'s `KnownNetworks.Clear()` / `KnownProxies.Clear()` | `Request.IsHttps` is false behind the gateway, so this config value is the only thing setting `Secure` (S14) |
| `ErrorThreeAttempts`'s `==` on the threshold | `SetAttempts`' remove-then-set, and the `AuthAttempts` primary key | A lost update past the threshold means the durable lock row is never written (§8.1, S6) |
| `MaxFailedAttemptsBeforeLockout` / `AccountLockDurationInMinutes` | The 10-minute HybridCache expiry on `user_attempt_{id}`, and `AuthServiceFixture`'s hardcoded `4`/`60` | The cache window and the lock duration interact; the fixture pins neither to production values (§8.1, §10) |
| `MaxOtpAttempts` | `ResendOtpAsync`'s `AttemptCount = 0`, and the unthrottled `/auth/resend-otp` route | The cap is renewable without limit; raising the constant changes nothing (§7.2, §7.3, S10) |
| `OtpVerificationResponse` | `RegisterEndpoint`'s `Results.Ok(…)`, and `RegisterService`'s `Adapt<>` | The record is the HTTP body verbatim — removing `OtpCodeHash`/`PasswordHash` is the fix for **S1** (§7.1) |
| `HashService.Hash`'s encoding, or `OtpService.GenerateOtp` | `RefreshTokenService.HashToken`, `GetAccessTokenIntegrationTests`, `AtsEmailAccountManagementService` (4 OTP call sites) | Two SHA-512 encodings coexist, and one shared OTP generator backs two unrelated policies (§7.2, S15) |
| `PasswordHasherService`'s constants | The stored format `$"{salt}.{hash}"` and `VerifyPassword`'s `parts.Length != 2` | No algorithm/version marker, so any parameter change invalidates every hash with no migration path (S15) |
| `GetUserTokenAsync` or `IsTokenValidInternal` | `ResetPasswordAsync`'s second `GetUserTokenAsync` call | Validation and consumption are two reads with a password write between them (**C6**, §7.4) |
| `ForgotPasswordAsync`'s early `return true` | The `throw` on send failure, and `IsUserEmailExistAsync`'s `IsActive` filter | Three distinguishable outcomes for one endpoint; the enumeration fix is incomplete (**C5**, §7.4) |
| Any exception type thrown from an Auth service | `CustomExceptionHandler`'s switch | `UnauthorizedAccessException` is unmapped → 500. Use the platform's `UnauthorizedException` for a real 401 (§9.3, S8) |
| `LoggingBehavior`'s `{@Request}` / `{@Response}` | Every command DTO in every module | An open behaviour with no redaction; Auth is not special, it is just where the secrets are (§9.1, S4) |
| `AddAuthorization()` or any `RequireAuthorization()` | `Features/UserManagement/**` — all 13 command slices | There is no policy to name; adding one is the fix for **S2** (§3.5, S2) |
| `AddAppSubRoleDTO` | `AppSubRoleService.AddAppSubRoleAsync`, and the caller-supplied `AssignedBy` | No authority check exists to update; `AssignedBy` is the only attribution and the client owns it (S2) |
| `AuthPaths.cs` route metadata | `GatewayServiceExtensions.AddRateLimiting`'s `switch` | `LoginPolicy` partitions on the policy name, not the IP — adding it to another route widens the shared bucket (§8.2, S5) |
| `SSOMetadata:SigninScheme`, or `AddSaml2`'s `AllowUnsolicitedAuthnResponse` | The four `GetValue<string>` reads of the scheme, and whether any policy ever includes it | The coupling is configuration, not a reference. Bounded today only because the cookie authenticates nothing; adding the scheme to a policy moves **S13** from Medium to Critical (§8.4, §12.4) |
| `Authusers.Email`'s index or uniqueness | `RegisterAsync`'s guard, `GetUserDataAsync`, `IsUserEmailExistAsync`, `GetRawUserAsync` | All four resolve duplicates with an unordered `FirstOrDefaultAsync` (§1.1, §7.1, S11) |
| `AuthRefreshToken.Id`'s type | `OnTokenValidated`'s `int.TryParse`, `IAuthSessionValidator.IsActiveAsync`'s signature, `sid`'s claim formatting | The `int` identity column is why `sid` is an integer claim end to end (§1.2, §2.3, §3.4) |
