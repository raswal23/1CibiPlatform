# Auth user status management — code explanation

Companion to [`auth-user-status-management.md`](auth-user-status-management.md). That one is
for deciding whether and why; this one is for changing the code.

## 1. The full call chain: deactivating a user

### Frontend

**`UI/FrontendWebassembly/Pages/Auth/UserAppRoles.razor`** — the User tab
(`_activeIndex == 2`) renders a `TableComponent<UsersDTO>` with `ColumnCount="7"`. The two
new columns and the action:

```razor
<MudTd DataLabel="Active">
    <span class="ats-status-pill @(user.isActive ? "done" : "error")">
        @(user.isActive ? "Active" : "Inactive")
    </span>
</MudTd>
<MudTd DataLabel="Action" Class="um-actions-col">
    <MudIconButton Icon="@Icons.Material.Filled.ManageAccounts" ...
                   OnClick="@(() => OpenEditUserStatusDialog(user))" />
</MudTd>
```

The badge is the **shared** `.ats-status-pill` from `wwwroot/css/ats.css`, the same chip the
orders board's `.report-status` geometry and every ATS status board use. Both columns use
`done` for the good state and `error` for the blocked one — `error` is the same
`--c-danger-bg` / `--c-danger-strong` pair the orders board gives a withdrawn application
(`.report-status.withdrawn`), so "this account cannot sign in" and "this application was
withdrawn" read identically across the app.

Those rules were previously scoped `.ats-management-page .ats-status-pill`, which this
`um-*` / `user-management-panel` screen is not inside. Rather than copy the block (the
failure the guide's "Reuse existing styles" section names), the **selector was generalized**
in the same change: the `.ats-management-page` prefix was dropped and the two
`--management-*` colour references swapped for the `--c-*` tokens they already resolved to,
so the rules no longer depend on that scope declaring them. No visual change to the existing
boards. `UserAppRoles.razor.css` therefore declares nothing for these columns.

**`UserAppRoles.razor.cs` → `OpenEditUserStatusDialog(UsersDTO)`** opens the dialog directly
rather than through `CrudPageBase.OpenEditDialogAsync`, because that helper expects the
dialog to close with the same DTO it was given and this one closes with an
`EditUserStatusDTO`. On a non-cancelled result it calls `ExecuteAndReloadAsync`, which owns
the failure snackbar and the table reload.

**`Component/UserManagement/EditUserStatusComponent.razor{,.cs}`** — built on
`UserManagementDialogShell`. It holds a local `IsActive` seeded in `OnParametersSet`, and
`HasChanged => IsActive != User.isActive` gates the Save button. `Submit` closes with
`DialogResult.Ok(new EditUserStatusDTO { UserId, IsActive })`.

There is **no `.razor.css`**. Every class it uses already exists in
`UserManagementDialogShell.razor.css`: `.um-status-row`, `.um-status-copy`,
`.um-status-badge`, `.um-toggle` (previously declared but unused) and
`.approval-field-display` / `.approval-email` / `.approval-verified` from the approval
dialog. Two rules were *added* to that shell sheet — `.approval-verified.is-off` and
`.um-inline-note` — rather than duplicated into a new file.

**`Services/Auth/Implementation/UserManagementService.cs`**:

```csharp
public Task<ServiceResponse<EditUserStatusDTO>> EditUserStatusAsync(EditUserStatusDTO editUserStatusDTO)
{
    var editUserStatus = new EditUserStatusDTO { ... };
    return PatchForAsync<EditUserStatusDTO>("auth/edituserstatus", new { editUserStatus });
}
```

The anonymous wrapper property name `editUserStatus` **must** match the backend's
`EditUserStatusRequest(EditUserStatusDTO editUserStatus)` parameter name — nothing enforces
that across the two projects at compile time. Same convention as `edituser`.

### Gateway

**`BackendAPI/Modules/Auth/Path/AuthPaths.cs`** — `EditUserStatusEntryPoint`, `Patch`,
`PathSet` → `/auth/edituserstatus`. `PathSet` (not `PathPattern`) because the route has no
`{parameter}`. Routes are read at startup only.

### Backend

**`Features/UserManagement/Command/EditUserStatus/EditUserStatusEndpoint.cs`** maps
`PATCH auth/edituserstatus` → `EditUserStatusCommand` → `ISender.Send`.

**`EditUserStatusHandler.cs`** holds the command, the `AbstractValidator` (`UserId`
`NotEmpty`), the result, and a handler that calls one service method. Validation runs
through `ValidationBehavior<,>`; nothing is registered by hand.

**`Services/UserManagement/UserService.cs` → `EditUserStatusAsync`** is where the two
decisions live:

```csharp
var existingUser = await _authRepository.GetUserByIdAsync(userStatusDTO.UserId);
if (existingUser == null)
    throw new NotFoundException($"User {userStatusDTO.UserId} was not found.");

existingUser.IsActive = userStatusDTO.IsActive;

var user = await _authRepository.EditUserAsync(existingUser);
```

`GetUserByIdAsync`, not `GetRawUserAsync` — the latter filters `au.IsActive`, so it can
never return the inactive user a reactivation is about. And `EditUserAsync` for the save,
because that is the method the cache decorator overrides to invalidate.

No try/catch: `CustomExceptionHandler` turns the `NotFoundException` into a 404
ProblemDetails, per the guide.

**`Data/Repository/UserManagement/AuthRepository.UserManagement.cs`**:

```csharp
public async Task<Authusers> GetUserByIdAsync(Guid id)
    {
        return await _dbcontext.AuthUsers
                     .Where(au => au.Id == id)
                     .FirstOrDefaultAsync();
    }
```

**`Data/Cache/UserManagement/AuthCacheRepository.UserManagement.Cache.cs`** — plain
pass-through, deliberately uncached (a single-row read taken immediately before a write).
The invalidation that matters is already in the decorator's `EditUserAsync`:

```csharp
if (updated != null)
    await _hybridCache.RemoveByTagAsync(UsersTag);
await _hybridCache.RemoveByTagAsync(UnApprovedUsersTag);
```

## 2. The read path diff

`GetUsers` is unchanged except for its query and projection, both in
`AuthRepository.UserManagement.cs`:

```csharp
private IQueryable<Authusers> BuildUsersQuery(string? searchTerm)
{
    var usersQuery = _dbcontext.AuthUsers
        .AsNoTracking();          // was: .Where(au => au.IsApproved == true && au.IsActive)
```

and `GetPageAsync` now projects `au.IsActive` as the seventh positional argument of
`UsersDTO`. `Auth/DTO/UsersDTO.cs` and `UI/FrontendWebassembly/DTO/Auth/UsersDTO.cs` are two
independent declarations of the same shape — the backend is a positional record, the
frontend a property record deserialized by name.

## 3. Wiring not visible from one file

| Thing | Where | Note |
|---|---|---|
| Carter endpoint discovery | assembly scan | No per-slice registration. |
| Gateway route | `Path/AuthPaths.cs` | Startup-only; restart to pick up. |
| Cache invalidation | `AuthCacheRepository.UserManagement.Cache.cs` → `EditUserAsync` | Reached only because the service saves through `EditUserAsync`. |
| Wrapper property name | UI `new { editUserStatus }` ↔ `EditUserStatusRequest(... editUserStatus)` | Must agree by string. |
| AppSubRole user list | `UserAppRoles.razor.cs` → `LoadAppSubRoleReferenceDataAsync` | Holds the `isActive && isApproved` filter the query used to. |

## 4. Change X, also check Y

| If you change… | Also check |
|---|---|
| `BuildUsersQuery` | `LoadAppSubRoleReferenceDataAsync`'s filter; `GetUsers_*` integration count assertions (currently 5 seeded users) |
| `UsersDTO` (either copy) | The other copy, `GetPageAsync`'s projection, and every `new UsersDTO(...)` in `Auth.UnitTests/UserManagementServiceTests.cs` |
| `EditUserStatusAsync` | `UserManagementServiceTests` (4 cases) and `UserManagementIntegrationTests` (3 cases) |
| The `auth/edituserstatus` route | `AuthPaths.cs` **and** the UI service path string |
| `.um-status-*` / `.um-toggle` | `UserManagementDialogShell.razor.css` — shared with anything else built on the shell |
| `.ats-status-pill` in `ats.css` | Now global, not scoped to `.ats-management-page`: the ATS boards **and** this Auth screen render it |
| `GetUserByIdAsync` / `GetRawUserAsync` | `ForgotPasswordService.ResetPasswordAsync` relies on the filtered one |
