# ATS Backend Authorization Gate

**Branch:** `feature/CIBI-Blue-Extraction`
**Date:** 2026-09-14
**Scope:** All internal ATS web endpoints (`Features/Web/**`, `Features/AuditTrail/**`)

## What this change does

Previously, keeping a disabled ATS user out was enforced only by the frontend: `ATSLayout` calls `getmymodules`, gets an empty list for a disabled user, and redirects to `/access-denied`. The backend endpoints themselves used bare `.RequireAuthorization()`, so a user disabled in ATS (`ats.UserDetails.IsActive = false`) who still held a valid JWT could call any ATS API directly — and endpoints not routed through `AtsAccessScopeResolver` would still serve or mutate data.

Now every internal ATS web endpoint rejects such callers with **HTTP 403** (ProblemDetails, `Detail = "The current user does not have valid ATS access."`) before the handler runs.

## The rule

A caller passes the gate when **either**:

1. They are a **platform SuperAdmin** (`platformRoleId` claim contains 1). Super admins administer ATS without having an `ats.UserDetails` row — the same bypass `AtsAccessScopeResolver` and `UserManagementService` already apply.
2. They have at least one `ats.UserDetails` row with `IsActive = true` **and** an active role (`Role.IsActive`). This is exactly the predicate `AtsAccessClaimsProvider` applies at login when deciding whether to issue `atsRoleId`/`atsClientId` claims — the gate re-checks it per request, closing the window where a token issued before the disable stays valid.

Everyone else — disabled users, users with no ATS account, users whose only role was deactivated — gets 403.

## How it works

- **`IAtsActiveUserGuard` / `AtsActiveUserGuard`** (`BackendAPI/Modules/ATS/Services/AccessScope/`) — reads `ICurrentUser`, applies the rule above, throws `ForbiddenException` on failure. No try/catch; `CustomExceptionHandler` converts it to a 403 ProblemDetails. Registered scoped in `ATSServiceConfiguration` next to `IAtsAccessScopeResolver`.
- **`RequireActiveAtsUser()`** (`BackendAPI/Modules/ATS/Shared/AtsEndpointExtensions.cs`) — a `RouteHandlerBuilder` extension that adds `.ProducesProblem(403)` plus an endpoint filter resolving the guard from the request's DI scope. The filter runs after authentication/authorization and before the handler.
- Applied as `.RequireAuthorization().RequireActiveAtsUser()` on **57 endpoints**: all 55 authorized endpoints under `Features/Web/**` plus the 2 audit-trail endpoints.

The active-role lookup reuses `IATSUserRepository.GetActiveUserRoleIdsAsync`, which is cached per user (`user_active_roles_{userId}`, tags `[User, Role]`). `EditUserAsync` — the operation that disables a user — evicts `CacheTags.User`, so a disable takes effect on the user's very next request on the same instance.

## What is deliberately NOT gated

| Surface | Why |
|---|---|
| `Features/Web/` anonymous candidate flows — `AddApplicationFormData`, `GetEmailIdAndApplicationFormPath`, `WithdrawnApplicationForm` | Applicants are authorized by hash token, not ATS accounts (`.AllowAnonymous()`) |
| `Features/PublicApi/**` (8 endpoints, `api/public/ats/*`) | Machine integration accounts that may have no `ats.UserDetails` rows; gating would break live integrations |
| `ATSHub` (SignalR) | Deliberately unauthenticated (see its XML doc) |
| Background jobs | No HTTP context |
| `EmploymentVerification`'s `api/employment-verification/ats/in-progress` | Different module; its own authorization concern |

## Interaction with the frontend

- The frontend behavior is unchanged and still fails closed: `ATSLayout` and `SecurePageBase` treat any `getmymodules` failure (now a 403 instead of `200 []` for disabled users) as an empty module set and redirect to `/access-denied`.
- `GetMyAccessHandler` already threw the same `ForbiddenException` for claim-less callers; the gate adds the DB-backed check in front of it.
- A disabled user's `NotificationCenter` polling will receive 403s until the layout redirects — its service converts failures into `ServiceResponse.Failure` without throwing.

## Known limitation: cross-instance cache staleness

HybridCache runs L1-only (`DisableDistributedCache`, 10-minute expiry, per-instance). Tag eviction on the instance that processed the disable is immediate, but **another instance that already cached the user's active roles may keep passing them for up to 10 minutes**. Accepted for now; the fix, if ever needed, is enabling the distributed layer for this key or reading it uncached.

## Tests

- **Unit** — `Test/.../ATS.UnitTests/AtsActiveUserGuardTests.cs`: super-admin bypass (repo never called), missing/empty `UserId` → Forbidden, active roles → pass, empty roles → Forbidden with the exact message.
- **Integration** — `Test/.../ATS.IntegrationTests/AtsActiveUserGuardIntegrationTests.cs`: resolves the real guard against the test database using the scope's fake principal — no ATS account → Forbidden; seeded active account → pass; seeded disabled account → Forbidden.
- Full ATS suite (584 tests) passes; existing tests are unaffected because they call services directly and never traverse the endpoint filter.

## How to verify manually

1. Log in as a normal ATS user; confirm the console works.
2. Have an admin disable that user in ATS User Management.
3. The user's next API call (dashboard refresh, or `GET /ats/getusers` with their token) returns 403 ProblemDetails; on next navigation the frontend redirects to `/access-denied`.
4. A platform SuperAdmin still accesses all ATS screens.
5. A candidate's application-form link (anonymous flow) still works; `GET /publicapi/ats/packages` with an integration token still works.
