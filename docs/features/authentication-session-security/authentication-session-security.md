# Authentication session security

This document explains the schema-free authentication hardening introduced for 1CibiPlatform. It deliberately keeps the existing `AuthRefreshToken` table and the application's multiple-session behavior: logging out one browser revokes only that browser session, while other sessions remain active.

## Before and after

This section records the previous implementation, the security or reliability issue it caused, and the implemented fix. It is intended to make the change easier to review without suggesting that the original design was entirely wrong—the existing user ID, token hash, active flag, expiry, and revocation fields remain useful and are still used.

| Area | Before: previous implementation | Issue | After: implemented fix |
| --- | --- | --- | --- |
| Session identity | The JWT identified the user, while the refresh-token row was located mainly through `UserId` and `TokenHash`. The JWT was not linked to one refresh-token row. | Revoking the refresh row stopped refresh, but an already-issued JWT could continue working until its expiration. The API could not distinguish which browser session issued that JWT. | The existing `AuthRefreshToken.Id` is placed in the signed JWT as `sid`. API JWT validation checks that exact row before accepting a session JWT. |
| Multiple logins | Web login could reuse a refresh cookie merely because the browser already had one. | A stale cookie could be carried into a new login, and each successful login was not guaranteed to represent a clean, independent session. | Every successful web login creates a new refresh-token row. Each browser/device therefore has its own `sid`; other sessions remain active when one logs out. |
| Access-token revocation | Logout set the matching refresh-token row inactive and added a revoke reason. | This correctly prevented future refreshes, but did not reject the access JWT immediately. | Logout still uses the existing revocation fields, then invalidates `auth-session:{sid}`. `OnTokenValidated` rejects the access JWT for that revoked session. |
| Logout request | The UI sent `userId` and `revokeReason` in the request body. The service searched the user's refresh-token records for a matching token. | Both values were client-controlled even though the server already had the session credential in its HttpOnly cookie. Logout also produced errors when repeated or when cookies were missing. | Logout has no body. It hashes the refresh cookie, finds its exact active row, uses server-owned reason `UserLogout`, clears cookies in `finally`, and is idempotent. |
| Refresh rotation | The service loaded the refresh row, changed its hash and expiry in memory, and then saved it. | Two concurrent requests could both validate the same old token before either save completed. | `RotateRefreshTokenAsync` performs one conditional database update matching row ID, old hash, active status, and expiry. Only one concurrent request can succeed. |
| Refresh-token replay result | A failed refresh update produced a generic server exception. | A replay/race is an authentication failure, not an internal server failure. | Losing the atomic rotation returns `UnauthorizedAccessException`, allowing the client to treat the session as unauthenticated. |
| JWT uniqueness | Access JWTs did not explicitly contain a unique token identifier. | Tokens created with identical inputs close together were harder to distinguish in logs or future security controls. | Every access JWT receives a random `jti`; refreshed JWTs retain the same `sid` but receive a new `jti`. |
| Session lookup cost | Rejecting a revoked JWT would otherwise require querying PostgreSQL on every authenticated request. | Correct but unnecessarily expensive for the current single-instance deployment. | `AuthSessionValidator` uses the existing local `HybridCache` for 30 seconds with PostgreSQL as the source of truth. Logout, rotation, and password reset invalidate affected entries. |
| Browser token exposure | Browser login and refresh responses returned token strings, and the UI could use the access token when retrying a request. | JavaScript-readable bearer material increases the impact of an XSS bug. | Tokens are delivered through HttpOnly cookies. Compatibility DTO properties remain temporarily but contain empty strings; the UI retries using the refreshed cookie. |
| Password-reset storage | The value used in a reset link and the database lookup flow did not consistently separate the bearer token from its stored verifier. | A usable reset credential should not be recoverable from database contents or application logs. | A cryptographically random raw token is emailed, only its hash is stored, submitted raw tokens are hashed once for lookup, and token values are excluded from logs. |
| Unknown forgot-password email | The endpoint exposed a different failure when an email did not exist. | Attackers could use response differences to enumerate registered accounts. | Known and unknown emails return the same success behavior. An email is sent only for an existing account. |
| Password change after recovery | Resetting a password did not necessarily terminate existing browser sessions. | A stolen session could remain authenticated after the owner recovered the account. | A successful reset revokes all active refresh sessions for that user and invalidates their session-cache entries. |
| OTP guessing | The existing `AttemptCount` was available but invalid verification attempts were not capped. | An OTP could be guessed repeatedly during its validity window. | Five invalid attempts consume the OTP. Resending generates a replacement and resets `AttemptCount` and `IsUsed`. |
| Cookie consistency | Cookie path and SameSite/expiry behavior differed between login and refresh paths. | Different settings can cause cookies to be sent or retained inconsistently. | Auth cookies explicitly use `Path=/`, prefer `SameSite=Lax`, and access-cookie lifetime follows the configured JWT lifetime. |

### Before: simplified flow

```text
Login
  -> possibly reuse an existing refresh cookie
  -> issue JWT containing the user identity
  -> save or update refresh-token data

Authenticated request
  -> validate JWT signature and expiry
  -> no link from JWT to one refresh-token row

Logout(userId, reason from UI)
  -> search active tokens belonging to userId
  -> compare the cookie with stored hashes
  -> mark the matching refresh token inactive
  -> already-issued JWT remains usable until expiry
```

### Why `UserId + TokenHash` was not removed

The previous approach is still valuable:

- `UserId` identifies the account that owns the session;
- `TokenHash` safely verifies the refresh credential without storing its raw value;
- `IsActive`, `ExpiresAt`, `RevokedAt`, and `RevokedReason` describe session state.

The missing part was a stable link from an **access JWT** to one of those rows. `sid` supplies only that link. It does not replace `UserId` or `TokenHash`, and it is not a secret.

### After: simplified flow

```text
Login
  -> always create one refresh-token row
  -> use that row's existing Id as sid
  -> issue JWT containing user identity + sid + unique jti
  -> put access and refresh tokens in HttpOnly cookies

Authenticated request
  -> validate JWT signature and expiry
  -> validate auth-session:{sid}
  -> require the row to belong to the JWT user and remain active/unexpired

Logout (no request body)
  -> hash refresh cookie and find the exact row
  -> mark only that row inactive with reason UserLogout
  -> invalidate auth-session:{sid}
  -> delete cookies
  -> other browser sessions remain active
```

## Request and session model

```text
Successful web login
  -> create a new AuthRefreshToken row
  -> use its existing Id as the session id (sid)
  -> issue an access JWT containing sub + sid + jti
  -> store access and refresh tokens only in HttpOnly cookies

Authenticated request
  -> validate JWT signature, issuer, audience and expiry
  -> read sub (user) and sid (session)
  -> check auth-session:{sid} in local HybridCache
  -> on cache miss, verify the AuthRefreshToken row in PostgreSQL
  -> require matching user, IsActive=true and ExpiresAt > UTC now

Current-session logout
  -> hash the refresh cookie
  -> locate its exact active AuthRefreshToken row
  -> set IsActive=false, RevokedAt and server reason UserLogout
  -> invalidate auth-session:{sid}
  -> delete both cookies
```

## Why each change exists

### One new row for every successful web login

`LoginService.LoginWebAsync` no longer trusts or reuses a refresh cookie merely because it exists. A new row makes each browser/device independent and prevents a stale cookie from one account being attached to another account.

### `sid` and `jti` JWT claims

`JWTService.GetAccessToken` now accepts an optional session ID. Browser login and refresh pass `AuthRefreshToken.Id` as `sid`; every access token also receives a unique `jti`. `sub` still identifies the user. The row ID already exists, so no migration is needed.

`sid` is not a secret. It connects a signed access token to the server-side refresh session so a revoked session can be rejected before the JWT naturally expires.

### Local session cache with database fallback

`AuthSessionValidator` caches `AuthRefreshToken` session status under `auth-session:{sid}` for 30 seconds. The existing HybridCache configuration is local/in-memory. PostgreSQL remains the source of truth, so cache loss or an application restart cannot reactivate a revoked session.

This is suitable while the API runs as one instance. Before using multiple API replicas, enable a shared distributed cache or Redis for session status.

### JWT rejection after logout

`ServiceConfiguration.AddJwtAuthentication` uses `OnTokenValidated` to compare JWT `sid` and `sub` with the session row. A revoked or expired row fails authentication. Legacy API/SSO JWTs without a `sid` preserve their existing behavior.

### Cookie-derived, idempotent logout

The browser no longer supplies a user ID or arbitrary revocation reason. The refresh token is the session credential and its database row already contains the correct user. Logout uses the cookie hash for an exact lookup and always removes cookies. Repeating logout succeeds even when the token is missing or already revoked.

### Atomic refresh rotation

`RotateRefreshTokenAsync` conditionally updates a row only when its current hash still matches and it is active and unexpired. Exactly one update means success. Concurrent use of the same refresh token allows only one request to rotate it; the other request receives an unauthorized response.

This prevents replay races, but full historical reuse detection still requires a future token-history or token-family migration because the current row overwrites the old hash.

### Browser tokens remain HttpOnly

The response DTO retains empty `AccessToken` and `RefreshToken` fields temporarily for source compatibility with existing tests/clients, but browser login and refresh no longer return token values. The UI retries after refresh using the updated cookie rather than adding a JavaScript-readable bearer token.

### Password-reset token handling and session revocation

Password-reset email now carries the raw random token while PostgreSQL stores only its SHA-256 verifier. Validation hashes the submitted raw token. Token values were removed from structured log context.

After a successful password reset, every active refresh session for that user is revoked and its local session cache entry is invalidated. This is intentionally different from normal logout because password recovery may indicate account compromise.

### OTP attempt limit

Registration OTP verification uses the existing `AttemptCount` column. Five invalid attempts mark that OTP used; issuing a replacement OTP resets the counter. No schema change is needed.

## Main files changed

| Area | Files | Reason |
| --- | --- | --- |
| JWT claims | `Services/Login/JWTService.cs`, `IJWTService.cs` | Add stable `sid` and unique `jti` |
| Login/logout | `Services/Login/LoginService.cs`, `Features/Logout/*` | Create independent sessions and revoke from cookie |
| Refresh | `Services/RefreshTokens/RefreshTokenService.cs`, repository/cache partials | Preserve sid and rotate atomically |
| Session validation | `Services/RefreshTokens/AuthSessionValidator.cs`, API `ServiceConfiguration.cs` | Reject revoked JWT sessions |
| Recovery | `Services/PasswordRecovery/ForgotPasswordService.cs`, password-recovery repository/cache partials | Correct bearer-token hashing and revoke sessions |
| OTP | `Services/Registration/RegisterService.cs` | Enforce existing attempt counter |
| UI | Auth services/DTO and `InterceptorHandler.cs` | Cookie-only token handling and bodyless logout |

## Code review map (what changed and why)

Use this section as the line-level review guide. Line numbers move as the files evolve, so each entry names the stable method or event instead of a brittle numeric line.

| Review location | Code to inspect | Why it is needed |
| --- | --- | --- |
| `JWTService.GetAccessToken` / `GetClaims` | Optional `sessionId`, `sid`, and random `jti` claims | Associates a signed browser JWT with one existing refresh-token row while keeping non-browser/API tokens compatible. |
| `LoginService.LoginWebAsync` | `SaveRefreshTokenAsync` before JWT creation; saved `session.Id` passed to JWT | Each login becomes an independent session. No existing browser cookie is trusted or reused. |
| `LoginService.LogoutAsync` | Hash cookie, find exact row, set `UserLogout`, invalidate cache, delete cookies in `finally` | Prevents the client from selecting another user/session and makes repeat logout safe. |
| `RefreshTokenService.GetNewAccessTokenAsync` | `RotateRefreshTokenAsync` followed by a JWT with the same row ID | Keeps the same browser session (`sid`) but prevents two requests from successfully rotating the same old token. |
| `AuthRepository.RotateRefreshTokenAsync` | Conditional `ExecuteUpdateAsync` on ID, old hash, active flag, and expiry | Performs the replay-sensitive check and update as one database statement without adding a column. |
| `AuthSessionValidator.IsActiveAsync` | `auth-session:{sid}` HybridCache key plus database fallback | Avoids a database query on every request while PostgreSQL remains the authority. |
| API JWT `OnTokenValidated` | Compare JWT `sid` + user claim with validator result | Makes the already-issued access JWT stop working after its specific session is revoked. |
| `ForgotPasswordService` | Email raw token, store/lookup hash, revoke all sessions after reset | A database leak does not reveal usable reset links; a recovered account ejects potentially compromised sessions. |
| `RegisterService.VerifyOtpAsync` | Increment existing `AttemptCount`; consume at five failures | Limits guessing without a migration. |
| UI Auth/Refresh services and `InterceptorHandler` | Bodyless logout and cookie-based retry | Keeps bearer and refresh token material outside JavaScript-accessible state. |

## Database impact

There is **no database migration** in this change. It reuses these existing fields:

- `AuthRefreshToken.Id` as `sid`;
- `TokenHash`, `IsActive`, `ExpiresAt`, `RevokedAt`, and `RevokedReason` for rotation/revocation;
- OTP `AttemptCount` and `IsUsed` for the verification limit.

Reliable detection of an already-rotated refresh token at a later time is intentionally not claimed here. That feature would need token-family/history persistence and therefore a future schema decision.

## Verification notes

- Build the API and UI independently; alternate output directories can be used when Visual Studio locks normal build output.
- Auth unit tests cover `sid`/`jti`, atomic rotation calls, cache invalidation, and cookie-only responses.
- At the time of this change, compilation of the combined `Test.csproj` is blocked by unrelated ATS `DisputeOrderService` tests that omit its existing `IOrderHistoryService` constructor argument. This Auth change does not modify those ATS tests.

## Known follow-up work

- Add endpoint-level rate limiting for login, registration, OTP resend/verify, forgot password and refresh after deciding production thresholds.
- Add antiforgery protection if deployment requires `SameSite=None`; current browser cookies should prefer `Lax` wherever topology permits.
- Add token-family/history persistence only when reliable old-refresh-token reuse detection becomes necessary.
- Move session cache to Redis before running multiple API replicas.
- Remove the deprecated empty token properties from browser response DTOs in a separately versioned API/UI contract change.

## Review checklist

- Two browsers can log in as the same user and receive different `sid` values.
- Logging out browser A revokes only A's refresh row and access JWT.
- Browser B remains authenticated.
- Refresh keeps the same `sid` but creates a new `jti` and refresh-token hash.
- Two concurrent refresh attempts with one old token produce only one successful rotation.
- Cache eviction/restart reloads active state from PostgreSQL.
- Password reset revokes all sessions.
- Raw access/refresh/reset tokens never appear in application logs.
