# Deactivation Guard: Blocking "In Use" Records

**Branch:** `feature/CIBI-Blue-Extraction`
**Date:** 2026-09-11
**Scope:** ATS Client Management, Package Management, Role Management, Module Management

## What this change does

The four ATS management screens let an admin flip a record's **Active** status inside the Edit dialog. Previously nothing checked whether the record was still in use, so a client with assigned users — or a package, role, or module actively referenced — could be silently disabled and break downstream flows (order placement, user access resolution, assignment dropdowns).

Now the **backend blocks the deactivation** when the record has active dependents and returns an error, which the frontend shows through the existing **error snackbar**. The exact message includes the live usage count, for example:

> Cannot disable this client: 3 active users are assigned to it.

## Block rules (what counts as "in use")

Only **active** dependents block deactivation. Historical or completed orders never block, so a record can always be retired once its dependents are deactivated first.

| Record being disabled | Blocked while… | Counted from |
|---|---|---|
| Client | Active ATS users are assigned to it | `ats.UserDetails` (`ClientId` + `IsActive`, distinct users) |
| Package | Active clients have it assigned | `ats.ClientDetails` (`PackageId` + `IsActive`) |
| Role | Active users currently hold it | `ats.UserDetails` (`RoleId` + `IsActive`, distinct users) |
| Module | Active users currently have it | `ats.UserDetails` (`ModuleId` + `IsActive`) |

The guard runs **only on an active → inactive transition**. Renaming or editing a record that stays active (or stays inactive) never triggers the check. Re-activating is always allowed.

Concurrency note: this is check-then-write. For an admin screen that is acceptable — a racing assignment merely produces a record disabled a moment too late.

## How it flows end to end

1. Admin opens the Edit dialog, toggles the status switch off, and hits **Save** (`PATCH /ats/editclient|editpackage|editrole|editmodule`).
2. The management service detects the active→inactive transition, calls the new repository count query (always fresh — the cache decorator does not cache it), and throws `ConflictException` if the count is above zero. No try/catch anywhere — per `docs/feature-development-guide.md`, `CustomExceptionHandler` converts the exception into an HTTP **409** ProblemDetails whose `Detail` is the message.
3. The frontend service reads `Detail` via the existing `ReadErrorDetailAsync` and returns `ServiceResponse.Failure(...)`.
4. The page shows it with the standard `Snackbar.Add(response.ErrorDetail, Severity.Error)`. On success, the usual success snackbar and table reload happen instead.

## Backend changes

### Repository count queries (one per module)

New interface methods + implementations on the `ATSRepository` partials, all `AsNoTracking`:

| Method | Files |
|---|---|
| `CountActiveUsersAssignedToClientAsync(clientId, ct)` | `Data/Repository/Clients/IClientRepository.cs`, `ATSRepository.Clients.cs` |
| `CountActiveClientsUsingPackageAsync(packageId, ct)` | `Data/Repository/PackageManagement/IPackageRepository.cs`, `ATSRepository.Packages.cs` |
| `CountActiveUsersInRoleAsync(roleId, ct)` | `Data/Repository/Roles/IRoleRepository.cs`, `ATSRepository.Roles.cs` |
| `CountActiveUsersWithModuleAsync(moduleId, ct)` | `Data/Repository/Modules/IModuleRepository.cs`, `ATSRepository.Modules.cs` |

Client and Role counts use `Select(UserId).Distinct()` because `UserDetails` holds one row per `(UserId, ModuleId)`; the Module and Package counts are plain counts because their key already appears once per dependent.

`ClientId` on `UserDetails` is a loose reference (no FK is possible — `ClientDetails` is keyed by `(ClientId, PackageId)`), so this usage check must live in application code; the database cannot enforce it.

### Cache decorators

`ATSCacheRepository.{Clients,Packages,Roles,Modules}.Cache.cs` — plain pass-through delegation for the four count methods, deliberately **uncached** so the guard always sees current assignments. No cache invalidation changes were needed: the exception is thrown before any write, so the decorators' edit-path evictions are never reached.

### Service-layer guards

Each management service gained the same transition-only block, placed after the existing NotFound check and before any mutation:

- `Services/Settings/ClientManagement/ClientManagementService.cs` — `EditClientAsync`
- `Services/Settings/PackageManagement/PackageManagementService.cs` — `EditPackageAsync`
- `Services/Settings/RoleManagement/RoleManagementService.cs` — `EditRoleAsync`
- `Services/Settings/ModuleManagement/ModuleManagementService.cs` — `EditModuleAsync`

Messages (singular/plural handled):

| Module | Message (plural form) |
|---|---|
| Client | `Cannot disable this client: {N} active users are assigned to it.` |
| Package | `Cannot disable this package: {N} active clients currently have it assigned.` |
| Role | `Cannot disable this role: {N} active users currently hold it.` |
| Module | `Cannot disable this module: {N} active users currently have it.` |

### CancellationToken threading (Role/Module)

`IRoleManagementService.EditRoleAsync` and `IModuleManagementService.EditModuleAsync` were tokenless; both now take a `CancellationToken`, passed from `EditRoleHandler` / `EditModuleHandler` (matching the Client/Package services). The existing integration-test call sites were updated accordingly.

### Endpoint metadata

`EditClientEndpoint`, `EditPackageEndpoint`, `EditRoleEndpoint`, `EditModuleEndpoint` now declare `.ProducesProblem(StatusCodes.Status409Conflict)` so the OpenAPI surface documents the new response.

No DB migrations, no DI registrations, no gateway/`ATSPaths` changes.

## Frontend changes

**None functionally.** The blocked deactivation surfaces through the pre-existing error path: the edit services already carry the ProblemDetails `Detail` into `ServiceResponse.ErrorDetail`, and the four management pages already show failures via the MudBlazor snackbar. A popup-dialog variant was prototyped and then removed in favor of snackbar-only per product decision.

Cleanup note: two inert stub files, `UI/FrontendWebassembly/Component/Generic/ErrorPromptDialogComponent.razor.cs` and `.razor.css`, remain from the removed prototype because a process lock prevented deletion. They are excluded from the build by a commented `<ItemGroup>` in `FrontendWebassembly.csproj`. Once nothing holds them open, delete the two files and remove that csproj block.

## Tests

`Test/Test/BackendAPI/Modules/ATS.IntegrationTests/` — three new cases per module in `ClientManagementServiceIntegrationTests`, `PackageManagementServiceIntegrationTests`, `RoleManagementServiceIntegrationTests`, `ModuleManagementServiceIntegrationTests`:

1. **Blocked** — deactivating with an active dependent throws `ConflictException` with the exact message, and the record stays active in the database.
2. **Allowed** — deactivating succeeds when the only dependents are inactive.
3. **Guard skipped** — renaming while staying active succeeds even with active dependents.

Seed helpers create the FK-required rows (`UserDetails` needs a valid role and module). Full run: **39 passed, 0 failed** across the four suites.

## How to verify manually

1. Create a client and assign an active user to it (User Management).
2. Open Client Management → Edit that client → toggle **Active** off → Save.
3. An error snackbar appears with the count message and the client remains active.
4. Deactivate the user, retry step 2 — the client deactivates with the success snackbar.
5. Repeat the same pattern for a package (assign to an active client), a role, and a module (held by an active user).
