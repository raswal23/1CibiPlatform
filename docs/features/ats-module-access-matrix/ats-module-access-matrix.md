# ATS module access matrix

## What it does

The Add User and Edit User dialogs now offer only the modules that the **role being assigned** is
allowed to hold. Previously the module checklist depended solely on who was logged in, so the role
dropdown had no effect on it.

The rule comes from the ATS module access sheet maintained outside this repository.
`UI/FrontendWebassembly/ShareData/ATS/ModuleList.cs` is now the in-repo source of truth for it.

## The problem it solved

Module visibility was computed from the *actor*, not the *target*:

```csharp
_canViewAllModules = isPlatformSuperAdmin || atsRoleId == AtsRoleList.PlatformManagerId;
```

That flag gated the whole checklist. A Platform Manager or Super Admin therefore saw all sixteen
modules no matter which role they picked, and could grant Package Management, Role Management, Audit
Trail or Email Accounts to a Client Experience user. A Client Admin saw nine modules — correct for
themselves — and could grant **User Management** to a role the sheet excludes it from.

## The matrix

Eight modules are visible to every ATS role:

| Id | Module |
|---|---|
| 1 | Dashboard |
| 2 | New Order |
| 3 | Orders & Reports |
| 4 | Disputes |
| 5 | Withdrawn |
| 12 | AI Assistant |
| 13 | Bulk Uploads Status |
| 14 | Ticketing Status |

Beyond those:

| Role (id) | Additional modules | Total |
|---|---|---|
| Platform Manager (1) | 6, 7, 8, 9, 10, 11, 15, 16 — all of them | 16 |
| Client Admin (2) | 10 User Management | 9 |
| Service Delivery (3) | none | 8 |
| User (4) | none | 8 |
| Client Experience (5) | none | 8 |

Role ids and names match `BackendAPI/Modules/ATS/Data/DataSeed/ATSInitialData.cs` `GetATSRoles()`
exactly.

**Super Admin is not an ATS role.** It is the platform Auth role `RoleList.SuperAdminId = 1`, and it
enters through the actor gate (`canViewAllModules`), never through this matrix. The sheet lists it in
its Access column but not among its roles; that is not a contradiction, just two different ladders.

## How it works

Two independent gates, intersected:

- **Actor gate** — `IsVisibleForAdministration(moduleId, canViewAllModules)`, unchanged. What the
  logged-in admin may grant at all, from `RestrictedAdministrationModuleIds`.
- **Role gate** — `IsInRoleModuleSet(moduleId, atsRoleId)`, new. What the assigned role may hold.

The checklist shows the intersection. The chips show only the actor gate, so a module the user
already holds but the newly chosen role disallows stays selected and renders muted with a dashed
border. It can still be removed through its chip; it cannot be re-added, because it is no longer in
the menu.

Three deliberate behaviors:

1. **Filter, not pre-select.** A module outside the role's set is not offered at all, rather than
   offered unchecked. Granting it is not a choice the admin should have.
2. **No role selected yet shows the shared eight.** The Add dialog opens on `RoleId == 0`
   ("Select a role"). An unrecognised role id falls back to the same eight, so the fallback is
   conservative for both cases.
3. **Changing the role never strips access silently.** Existing selections that fall outside the new
   role are kept and marked, matching the long-standing rule in `EditUserComponent` that saving must
   not silently remove what the admin cannot see. `ToggleAllModules` in `AddUserComponent` was made
   additive to honour this; it previously replaced the whole selection with `[]` on deselect.

## Scope

UI only. Nothing in the backend changed:

- The API still accepts whatever module ids it is sent and still enforces its own rules. This change
  narrows what the dialogs offer; it does not narrow what the endpoint accepts.
- **Users already granted a module their role disallows keep it.** No backfill, no cleanup. The
  sidebar is driven by the backend's `GetMyModules`, so such a user still sees that module until the
  grant is removed by hand.
- Role Management has no module-assignment UI, so these dialogs are the only surface the matrix
  applies to.

## How to verify

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AtsModuleListTests"
dotnet build 1CibiPlatform.sln
```

`Test/Test/UI/AtsModuleListTests.cs` pins the matrix above, including the fallbacks and the
invariant that the actor gate equals the Client Admin role set.

On screen, as a Super Admin or Platform Manager: open **Add User**, pick **Client Experience**, and
confirm the module menu lists exactly the eight shared modules. Pick **Platform Manager** and confirm
all sixteen appear. In **Edit User**, take a user holding Audit Trail, switch their role to
**Service Delivery**, and confirm the Audit Trail chip stays but turns muted and dashed, and that
Audit Trail is gone from the menu.

The muted-chip styling is a visual change and is **not verified by the build or the tests** — it needs
eyes on the dialog in light and dark mode.

## What not to do

- **Do not widen the checklist by relaxing the actor gate alone.** `IsSelectableForUserRole` ANDs the
  two gates; passing `canViewAllModules: true` still cannot offer a module the target role lacks.
- **Do not filter the chips by role.** Gating `SelectedModuleChips` on the role would hide
  already-granted modules and re-introduce the silent strip the third behavior exists to prevent.
- **Do not add a module to `ModuleList.List` without deciding its role set.** A new id absent from
  `AllRoleModuleIds` and from every `RoleOnlyModuleIds` entry is grantable by nobody. Add it to the
  Platform Manager entry at minimum, and to `AllRoleModuleIds` if every role should see it.
- **Do not treat `RestrictedAdministrationModuleIds` and the matrix as unrelated.** The first must
  equal the complement of the Client Admin set; `IsVisibleForAdministration_ShouldMatchTheClientAdminRoleSet_WhenActorCannotViewAllModules`
  fails if they drift.
- **Do not conflate `AllRoleModuleIds` with `IsPrimaryNavigationModule`.** The two sets are identical
  today, but one is about access and the other about sidebar placement. They will diverge the first
  time an administration module is promoted to the primary nav.

## Known naming mismatch

The access sheet calls module 16 **Email Management**; `ModuleList` and the UI call it **Email
Accounts**. Left as-is — renaming a module touches the seed data, which is out of scope here.
