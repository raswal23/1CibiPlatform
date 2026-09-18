# Auth user approval rejection — code explanation

Companion to [`auth-user-approval-rejection.md`](auth-user-approval-rejection.md).

## 1. The full call chain: disapproving a pending user

### Frontend

**`Component/UserManagement/EditUserApprovalComponent.razor`** — the footer's two buttons now
call different handlers. Before this change `Disapprove` called `Cancel`, which is why it did
nothing:

```razor
<button type="button" class="um-button um-button-danger" @onclick="Disapprove">Disapprove</button>
<button type="button" class="um-button um-button-save" @onclick="Submit">Approve</button>
```

**`EditUserApprovalComponent.razor.cs`** closes with a decision rather than the DTO, because
approve and reject are different calls and the user record alone cannot say which button was
pressed:

```csharp
public enum UserApprovalAction { Approve, Reject }
public record UserApprovalDecision(UserApprovalAction Action, UnApprovedUsersDTO User);
```

`Submit` validates the form and closes `Ok(new UserApprovalDecision(Approve, User))`;
`Disapprove` closes `Ok(...Reject, User)` with no validation — the only field is a read-only
email, and rejection is about the user as they already are. `Cancel` still calls
`MudDialog.Cancel()`, so the X, the backdrop and Cancel stay distinct from a rejection.

**`Pages/Auth/UserAppRoles.razor.cs` → `OpenEditUserApprovalDialog`** opens the dialog
directly (not via `CrudPageBase.OpenEditDialogAsync`, which assumes the dialog returns the
DTO type it was handed) and branches:

```csharp
if (result is null || result.Canceled || result.Data is not UserApprovalDecision decision)
    return;

if (decision.Action == UserApprovalAction.Reject)
{
    await RejectUser(decision.User);
    return;
}

await EditUser(decision.User);
var notificationResponse = await UserManagementService.SendApprovalNotificationAsync(decision.User.email!);
```

**`RejectUser`** confirms through `ShowUserManagementConfirmationAsync` (the shared
`ConfirmationDialogComponent`, also used by account unlock) and then calls
`ExecuteAndReloadAsync`, which owns the error snackbar and the table reload.

**`Services/Auth/Implementation/UserManagementService.cs`**:

```csharp
public Task<ServiceResponse<bool>> RejectUserAsync(Guid userId)
    => SendForBoolAsync(() => _httpClient.DeleteAsync($"auth/rejectuser/{userId}"));
```

Same shape as `DeleteLockedUserAsync` beside it.

### Gateway

**`Path/AuthPaths.cs`** — `RejectUserEntryPoint`, `Delete`, `MatchPath`
`/auth/rejectuser/{UserId}` with `{ "PathRemovePrefix", "/auth/" }`, matching
`DeleteApplicationEntryPoint` and the other parameterised deletes.

### Backend

**`Features/UserManagement/Command/RejectUser/RejectUserEndpoint.cs`** maps
`DELETE auth/rejectuser/{UserId}` and returns the bare `bool`, following
`DeleteApplicationEndpoint`.

**`RejectUserHandler.cs`** — command, validator (`UserId` `NotEmpty`), result, handler.

**`Services/UserManagement/UserService.cs` → `RejectUserAsync`**:

```csharp
var existingUser = await _authRepository.GetRawUserAsync(userId);
if (existingUser == null)
    throw new NotFoundException($"User {userId} was not found.");

if (existingUser.IsApproved)
    throw new BadRequestException("This user has already been approved.");

existingUser.IsActive = false;
await _authRepository.EditUserAsync(existingUser);
```

`GetRawUserAsync` here — the *filtered* lookup — is correct and load-bearing: it excludes
inactive users, so a second rejection of the same person is a 404 rather than a silent
success. (Status editing needs the opposite and uses `GetUserByIdAsync`; see the
[status management companion](../auth-user-status-management/auth-user-status-management_code_explanation.md).)

Saving through `EditUserAsync` is what reaches the cache decorator's
`RemoveByTagAsync(UsersTag)` / `(UnApprovedUsersTag)`, so the Approval tab drops the row
immediately instead of serving a cached first page.

## 2. Wiring not visible from one file

| Thing | Where |
|---|---|
| Gateway route | `Path/AuthPaths.cs` — startup-only |
| Cache invalidation | reached via `EditUserAsync`, not written here |
| Confirmation dialog | `ShowUserManagementConfirmationAsync` → `ConfirmationDialogComponent` |
| Which lookup filters `IsActive` | `GetRawUserAsync` yes, `GetUserByIdAsync` no |

## 3. Change X, also check Y

| If you change… | Also check |
|---|---|
| `UserApprovalDecision` / the enum | `OpenEditUserApprovalDialog`'s branch in `UserAppRoles.razor.cs` |
| `RejectUserAsync` guards | `UserManagementIntegrationTests`; the already-approved case is the easy one to drop |
| The `auth/rejectuser` route | `AuthPaths.cs` **and** the UI service path string |
| `GetRawUserAsync`'s filter | This method's double-rejection guard depends on it, and so does `ForgotPasswordService` |
| Rejection to hard-delete | The User tab's reactivation path assumes the row still exists |
