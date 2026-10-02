# Auth user status management

## What it does

The **User** tab of User Management (`/userapproles`, tab index 2) lists every registered
account and lets an administrator activate or deactivate one. Two columns report state at a
glance — **Approved** and **Active** — and a per-row action opens a dialog whose only
control is the activeness toggle.

Deactivating is how an account is retired without deleting it: `Authusers.IsActive = false`
means the person can no longer sign in, while their history, assignments and audit trail
stay intact.

## Why the list is no longer filtered

`AuthRepository.BuildUsersQuery` used to filter `IsApproved == true && IsActive`. That was
survivable while the tab was read-only, and impossible once it could deactivate: the moment
an admin switched a user off, the row left the only screen able to switch them back on.

The tab is now the whole registry, and state is **shown rather than filtered**. That is
also why the Approved column exists — an unapproved user appears here too, and reporting
that is more useful than hiding them.

`BuildUnapprovedUsersQuery` is deliberately untouched. The Approval tab still filters
`IsApproved == false && IsActive`, because a rejected (deactivated) user should not reappear
in a queue of pending decisions. See [`auth-user-approval-rejection`](../auth-user-approval-rejection/auth-user-approval-rejection.md)
for how rejection deactivates.

## What a status edit can and cannot touch

Only `IsActive`. The dialog displays name, email and approval state read-only.

| Field | Why it is out of reach |
|---|---|
| `IsApproved` | Belongs to the Approval tab. A status edit that could approve would be a second, undocumented path to the same decision. |
| Name / email | Belong to the user, changed through their own profile. |

The dialog's Save button stays disabled until the toggle actually moves, so confirming an
unchanged status costs neither a request nor a table reload.

## Approved and active are independent

Both must hold for a sign-in. An unapproved user cannot sign in however active they are,
which is why the dialog shows an inline note when activating one — otherwise flipping the
toggle looks like it granted access that the user still does not have.

| Approved | Active | Can sign in |
|---|---|---|
| Yes | Yes | Yes |
| Yes | No | No — deactivated |
| No | Yes | No — `LoginService` refuses unapproved accounts |
| No | No | No |

Both columns therefore use the same two colours: green for the state that permits a sign-in,
and red — the shared pill's `error` modifier, matching a withdrawn application on the orders
board — for either state that blocks one. Anything red in these columns means the person
cannot get in, whichever column it is in.

The dialog's own badge and toggle keep the neutral grey the shell already used for off,
rather than the table's red. Those rules in `UserManagementDialogShell.razor.css` are shared
with the Add/Edit Application and Add/Edit SubMenu dialogs, so a change there reaches four
other screens that toggle a different kind of flag.

## Assignment still wants the old filter

`GetUsersAsync` feeds two consumers: this tab and the **Add/Edit User's AppSubRole**
autocomplete. Widening the query would have offered deactivated and unapproved users as
assignment targets, granting application roles that cannot be used and obscuring the real
reason the person cannot sign in.

The filter therefore moved to the one caller that needs it —
`LoadAppSubRoleReferenceDataAsync` filters `isActive && isApproved` — rather than staying in
the query where it constrained everybody.

### The same split now applies to applications and submenus

The Application and SubMenu tabs took this pattern after the User tab proved it. Both
`BuildApplicationsQuery` and `BuildSubMenusQuery` used to filter `IsActive`, which made an
inactive record invisible on the only screen that could switch it back on — the same trap
`BuildUsersQuery` had. Both now return the full registry and report state through an
**Active** column, and `LoadAppSubRoleReferenceDataAsync` filters all three lists:

```csharp
_appSubRoleApplications = applications.Items.Where(a => a.IsActive)…
_appSubRoleSubMenus     = subMenus.Items.Where(s => s.IsActive)…
_appSubRoleUsers        = users.Items.Where(u => u.isActive && u.isApproved)…
```

Those `Where` clauses are load-bearing, not tidying. The repository queries were what kept
switched-off records out of the assignment dropdowns, so removing the filter there without
adding it here would have made them assignable.

Roles have no such filter because `AuthRole` has no `IsActive` — the Role tab is name and
description only, deliberately.

The rule these enforce together: **the management tabs are the registry, the assignment
dialog is the subset you may assign.** A record stays visible and restorable wherever it is
administered, and unusable wherever it would grant access that does not work.

See [`auth-appsubrole-assignment`](../auth-appsubrole-assignment/auth-appsubrole-assignment.md)
for the assignment rules themselves, including the uniqueness constraint.

## How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~UserManagement"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Auth.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Auth.IntegrationTests"
dotnet build 1CibiPlatform.sln
```

The gateway reads its routes at startup, so restart `apis` and `apigateway` and confirm
`GET /__routes` lists `EditUserStatusEntryPoint`.

Correct looks like: both pills render on every row; deactivating a user leaves the row in
place with an Inactive pill; reactivating that same user succeeds; and the deactivated user
no longer appears in the AppSubRole user autocomplete.

The same three checks apply to the Application and SubMenu tabs: an inactive record keeps its
row and shows an Inactive pill, can be switched back on from the Edit dialog, and is absent
from the AppSubRole dropdowns while off.

## What not to do

| Don't | Because |
|---|---|
| Re-scope `.ats-status-pill` under `.ats-management-page` | This Auth screen is not inside that wrapper; the badges would lose their styling and the next screen would copy the block instead. |
| Re-add `IsActive` or `IsApproved` to `BuildUsersQuery` | A deactivated user disappears from the only screen that can restore them. |
| Use `GetRawUserAsync` for the status load | It filters `IsActive`, so every reactivation answers 404. `GetUserByIdAsync` exists for this. |
| Relax `GetRawUserAsync`'s filter instead | Password recovery relies on it; a deactivated account must not be resettable. |
| Let the status path write `IsApproved` | Approval is the Approval tab's decision, and two write paths to one flag will disagree. |
| Save without going through `EditUserAsync` | That is where the cache decorator invalidates `UsersTag`/`UnApprovedUsersTag`; a direct save leaves the board showing a stale first page. |
| Drop any of the `IsActive` filters in `LoadAppSubRoleReferenceDataAsync` | They are the only thing keeping switched-off users, applications and submenus out of the assignment dropdowns now that the repository queries no longer filter. Roles are the one list with no filter, because `AuthRole` has no `IsActive`. |
| Re-add `.Where(x => x.IsActive)` to `BuildApplicationsQuery` or `BuildSubMenusQuery` | Same trap as `BuildUsersQuery`: an inactive record vanishes from the only screen that can restore it, and the Active column can then only ever read "Active". |
| Add `IsActive` to `AuthRole` to "match" the others | Nothing asked for it, and it is a schema change plus a migration for a flag no caller reads. |
