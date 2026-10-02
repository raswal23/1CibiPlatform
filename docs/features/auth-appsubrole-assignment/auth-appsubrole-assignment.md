# Auth AppSubRole assignment

## What it does

The **User's AppSubRole** tab of User Management (`/userapproles`, tab index 6) grants a user
a role on one submenu of one application. A row in `AuthUserAppRole` is that grant.

Two rules govern what may be created: an assignment must be **unique** per user, application
and submenu, and it may only reference records that are **switched on**.

## One assignment per user, per application, per submenu

`AppSubRoleService.AddAppSubRoleAsync` rejects a duplicate with `ConflictException` → 409:

> This user already has a role assigned for that application and submenu. Edit the existing
> assignment to change their role.

**`RoleId` is deliberately not part of the key.** Granting the same person a second role on
the same submenu *is* the duplicate worth blocking — which of the two applies is undefined,
and the list renders both rows identically apart from a hidden id, so an operator cannot tell
them apart or know which to delete. Changing someone's role is an edit, not a second grant.

### The edit path is guarded too

`EditAppSubRoleAsync` runs the same check, passing `excludeAppRoleId` so a row does not
collide with itself. Guarding only the add would leave the rule with a back door: an operator
could edit an existing row onto a taken combination and produce exactly the duplicate the add
path refuses.

The exclusion is what keeps the ordinary use of the dialog working — changing the role on an
existing assignment is a save onto the same three key fields.

### The check is deliberately uncached

`AuthCacheRepository.AppSubRoleExistsAsync` passes straight through to the repository, unlike
the list reads beside it. This call decides whether a write is allowed, and a cached "no
duplicate" would stay true for the life of the entry — long enough for two administrators to
each be told the slot is free and both take it.

### What it does not do

There is **no unique index** on `(UserId, AppId, Submenu)`. Two simultaneous requests can
still pass the check and both insert, and any duplicates already in the table are untouched.
Closing that needs a constraint plus a one-time cleanup of existing rows; the application-level
check is what stops the reachable case, which is one person clicking twice.

## Only assignable records are offered

`LoadAppSubRoleReferenceDataAsync` filters every list the dialog's dropdowns bind to:

| List | Filter | Why |
|---|---|---|
| Users | `isActive && isApproved` | An account that cannot sign in cannot use the role, and the grant hides the real reason. |
| Applications | `IsActive` | Same reasoning — access to a switched-off application does not work. |
| SubMenus | `IsActive` | As above. |
| Roles | none | `AuthRole` has no `IsActive`. |

These filters are load-bearing rather than cosmetic. The repository queries used to apply
them, and stopped once the management tabs became full registries — so removing them here
makes switched-off records assignable again. See
[`auth-user-status-management`](../auth-user-status-management/auth-user-status-management.md)
for why the tabs show state instead of filtering it.

## How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AppSubRole"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Auth.UnitTests"
dotnet build 1CibiPlatform.sln
```

Correct looks like: assigning a user a role on an application/submenu they already hold one
for is refused with the 409 message in a snackbar; assigning them a *different* role on that
same submenu is refused identically; assigning them a role on a different submenu succeeds;
and opening an existing assignment and changing only its role saves without complaint.

## What not to do

| Don't | Because |
|---|---|
| Add `RoleId` to the uniqueness key | It would permit two roles on one submenu, which is the ambiguity this prevents. |
| Guard the add path only | An edit can land on a taken combination just as easily. |
| Drop `excludeAppRoleId` on the edit check | Every save would collide with the row being saved, so no assignment could ever be edited. |
| Cache `AppSubRoleExistsAsync` | A stale "free" answer lets two administrators both create the same assignment. |
| Drop the `IsActive` filters in `LoadAppSubRoleReferenceDataAsync` | Nothing else keeps switched-off records out of the dropdowns now that the repository queries return full registries. |
| Assume the check makes duplicates impossible | There is no unique index; concurrent requests can still race, and pre-existing duplicates remain. |
