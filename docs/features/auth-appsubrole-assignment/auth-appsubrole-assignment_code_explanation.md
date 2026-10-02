# Auth AppSubRole assignment — code explanation

Companion to [`auth-appsubrole-assignment.md`](auth-appsubrole-assignment.md).

## 1. The pieces

| File | Role |
|---|---|
| `Data/Entities/UserAppRole.cs` | `AuthUserAppRole` — `AppRoleId` PK, the `UserId`/`AppId`/`Submenu` natural key, `RoleId` |
| `Data/Repository/AppSubRoles/AuthRepository.AppSubRoles.cs` | `AppSubRoleExistsAsync` — the lookup |
| `Data/Repository/AppSubRoles/IAppSubRoleRepository.cs` | its contract |
| `Data/Cache/AppSubRoles/AuthCacheRepository.AppSubRoles.Cache.cs` | pass-through, deliberately uncached |
| `Services/AppSubRoles/AppSubRoleService.cs` | the two guards, both throwing `ConflictException` |
| `Features/UserManagement/Command/AddAppSubRole/` | handler + FluentValidation (shape only) |
| `Features/UserManagement/Command/EditAppSubRole/` | same, for the edit |
| `Pages/Auth/UserAppRoles.razor.cs` | `LoadAppSubRoleReferenceDataAsync` — the dropdown filters |

## 2. The existence check

```csharp
public Task<bool> AppSubRoleExistsAsync(
    Guid userId, int appId, int subMenuId, int? excludeAppRoleId, CancellationToken cancellationToken) =>
    _dbcontext.AuthUserAppRoles
        .AsNoTracking()
        .AnyAsync(x => x.UserId == userId
                   && x.AppId == appId
                   && x.Submenu == subMenuId
                   && (!excludeAppRoleId.HasValue || x.AppRoleId != excludeAppRoleId.Value),
            cancellationToken);
```

`RoleId` is absent from the predicate on purpose — see the behaviour doc. `excludeAppRoleId`
is nullable so one method serves both paths: `null` on add (nothing to exclude), the row's own
id on edit.

The `!excludeAppRoleId.HasValue ||` form translates to SQL cleanly because EF evaluates the
captured nullable once; it does not produce a per-row branch.

## 3. Why the guard is in the service, not the validator

`AddAppSubRoleCommandValidator` checks shape only — non-empty ids, positive integers. It
cannot check uniqueness because a FluentValidation validator has no repository and runs
before the handler.

Putting it in the service also means the **edit** path gets it from the same place, rather
than two validators drifting apart.

## 4. Why the cache decorator passes through

Every other read in `AuthCacheRepository.AppSubRoles.Cache.cs` caches under `AppSubRolesTag`.
This one does not:

```csharp
// Deliberately uncached, unlike the list reads above. This one decides whether a write is
// allowed, and a cached "no duplicate" would stay true for the life of the entry.
public Task<bool> AppSubRoleExistsAsync(...) =>
    _authRepository.AppSubRoleExistsAsync(userId, appId, subMenuId, excludeAppRoleId, cancellationToken);
```

A cached negative is the dangerous direction: it reports the slot free after someone has taken
it.

## 5. The `CancellationToken` signature change

`AddAppSubRoleAsync` and `EditAppSubRoleAsync` gained a `CancellationToken` parameter, because
the new repository call takes one and the handlers already had it in scope. Both handlers pass
`cancellationToken` through; `IAppSubRoleService` was updated to match.

This is why `AppSubRoleServiceTests` needed edits beyond the new cases — three existing calls
had to supply `CancellationToken.None`.

## 6. A Moq trap in these tests

`AppSubRoleServiceTests` uses `IClassFixture<AuthServiceFixture>`, so **one mock is shared by
every test in the class** and its recorded invocations accumulate. A `Times.Never` assertion
with a broad matcher therefore fails depending on test order:

```csharp
// Wrong — counts another test's successful add
_fixture.MockAuthRepository.Verify(x => x.AddAppSubRoleAsync(It.IsAny<AddAppSubRoleDTO>()), Times.Never);

// Right — scoped to this test's own dto
_fixture.MockAuthRepository.Verify(x => x.AddAppSubRoleAsync(appSubRole), Times.Never);
```

The first form passes when run alone and fails in the suite, which is the confusing case.

## 7. How the message reaches the user

`ConflictException` → `CustomExceptionHandler` → `ProblemDetails.Detail` with status 409 →
`HttpResponseMessageExtensions.ReadErrorDetailAsync` reads `Detail` → `ServiceResponse.Failure`
→ `UserAppRoles.razor.cs` shows `response.ErrorDetail` in a snackbar.

No frontend change was needed: that chain already existed, so the exception message is what the
operator reads verbatim. Rewording the `throw` rewords the snackbar.

## 8. Change X, also check Y

| If you change… | Also check |
|---|---|
| The uniqueness key | Both call sites in `AppSubRoleService`, and the four `AppSubRoleServiceTests` cases that pin it |
| `IAppSubRoleService` signatures | `AddAppSubRoleHandler`, `EditAppSubRoleHandler`, and every call in `AppSubRoleServiceTests` |
| `AppSubRoleExistsAsync` | The cache decorator must keep passing through, not start caching |
| The exception type | `CustomExceptionHandler`'s mapping decides the status code the UI reports |
| `LoadAppSubRoleReferenceDataAsync` filters | `BuildApplicationsQuery` / `BuildSubMenusQuery` no longer filter — see `auth-user-status-management_code_explanation.md` §2.1 |
