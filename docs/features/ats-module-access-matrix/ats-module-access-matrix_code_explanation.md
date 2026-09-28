# ATS module access matrix — code explanation

Companion to [`ats-module-access-matrix.md`](ats-module-access-matrix.md). This file is for someone
about to change the code: what calls what, in the order it runs.

UI-only change. No backend file was touched.

## Files

| File | Role in this feature |
|---|---|
| `UI/FrontendWebassembly/ShareData/ATS/ModuleList.cs` | The matrix and the two gates |
| `UI/FrontendWebassembly/ShareData/ATS/AtsRoleList.cs` | ATS role id constants |
| `UI/FrontendWebassembly/Component/ATS/UserManagement/UserManagement.razor` | Opens both dialogs, submits the result |
| `…/UserManagement/AddUserComponent.razor` + `.razor.cs` + `.razor.css` | Create dialog |
| `…/UserManagement/EditUserComponent.razor` + `.razor.cs` + `.razor.css` | Edit dialog |
| `Test/Test/UI/AtsModuleListTests.cs` | Pins the matrix |

`UserManagement.razor` has **no `.razor.cs`** — its logic lives in an `@code` block, unlike the two
dialogs which follow the separated partial-class pattern.

## 1. The matrix

`ModuleList.cs:18` — the actor gate, pre-existing, unchanged in content:

```csharp
	private static readonly int[] RestrictedAdministrationModuleIds = [6, 7, 8, 9, 11, 15, 16];
```

`ModuleList.cs:24-36` — new:

```csharp
	private static readonly int[] AllRoleModuleIds = [1, 2, 3, 4, 5, 12, 13, 14];

	// Modules beyond AllRoleModuleIds that each role may hold. A role absent from this
	// dictionary - and role id 0, meaning no role has been picked yet - gets the all-roles
	// set alone.
	private static readonly Dictionary<int, int[]> RoleOnlyModuleIds = new()
	{
		[AtsRoleList.PlatformManagerId] = [6, 7, 8, 9, 10, 11, 15, 16],
		[AtsRoleList.ClientAdminId] = [10],
		[AtsRoleList.ServiceDeliveryId] = [],
		[AtsRoleList.UserId] = [],
		[AtsRoleList.ClientExperienceId] = []
	};
```

`ModuleList.cs:73-88` — the two new predicates:

```csharp
	// Whether the module belongs to the set a given ATS role may hold.
	public static bool IsInRoleModuleSet(int moduleId, int atsRoleId) =>
		AllRoleModuleIds.Contains(moduleId) ||
		(RoleOnlyModuleIds.TryGetValue(atsRoleId, out var roleOnlyIds) &&
		 roleOnlyIds.Contains(moduleId));

	// The Add/Edit User checklist gate: the actor may grant it, and the role being assigned
	// may hold it. A module already on the user that fails the role half stays selected and
	// is rendered disabled, so changing the role never silently strips existing access.
	public static bool IsSelectableForUserRole(
		int moduleId,
		bool canViewAllModules,
		int atsRoleId) =>
		IsVisibleForAdministration(moduleId, canViewAllModules) &&
		IsInRoleModuleSet(moduleId, atsRoleId);
```

`TryGetValue` returning `false` is what makes both `RoleId == 0` and an unknown id fall back to the
shared eight — there is no explicit branch for either.

`AtsRoleList.cs` gained the one constant it was missing:

```csharp
	public const int UserId = 4;
```

## 2. Trace: Add User, end to end

**Hop 1 — `UserManagement.razor` opens the dialog** (around line 309):

```csharp
        var parameters = new DialogParameters<AddUserComponent>
        {
            { component => component.AuthUsers, eligibleAuthUsers },
            { component => component.Roles, roles },
            { component => component.Modules, modules },
            { component => component.Assignments, userClientAssignments },
            { component => component.IsPlatformSuperAdmin, isPlatformSuperAdmin }
        };
```

then `await DialogService.ShowAsync<AddUserComponent>("Add User", parameters, options)`.
`modules` is the **full** module list from the API — the filtering happens in the dialog, not here.

**Hop 2 — `AddUserComponent.OnInitializedAsync` resolves the actor:**

```csharp
		var isPlatformSuperAdmin = await AccessService.HasRoleAsync(RoleList.SuperAdminId);
		var atsRoleId = await GetStoredATSRoleIdAsync();
		_canViewAllModules = isPlatformSuperAdmin || atsRoleId == AtsRoleList.PlatformManagerId;
```

`RoleList.SuperAdminId` is the **platform Auth** role (`ShareData/Auth/RoleList.cs`, value `1`);
`GetStoredATSRoleIdAsync` reads the `ATSRoleId` key from local storage and returns `0` on
`JsonException`. These two are the actor gate's only inputs. Note the coincidence that both
constants equal `1` — they are different ladders and must not be merged.

**Hop 3 — the role dropdown binds the target.** `AddUserComponent.razor:135`:

```razor
                                                   @bind-Value="User.RoleId"
```

with `<MudSelectItem T="int" Value="0" Disabled="true">Select a role</MudSelectItem>` above the
`AssignableRoles` loop. Changing this selection re-renders and recomputes every property below; no
change handler is involved.

**Hop 4 — the checklist narrows.** `AddUserComponent.razor.cs`:

```csharp
	private IEnumerable<ModuleDetailsDTO> VisibleModules => Modules
		.Where(module => ModuleList.IsSelectableForUserRole(
			module.ModuleId,
			_canViewAllModules,
			User.RoleId));
```

Rendered by `AddUserComponent.razor` inside the "Add module" `MudMenu`:

```razor
                                        @foreach (var module in VisibleModules.Where(module => module.IsActive))
```

**Hop 5 — selection.** `ToggleModule` → `OnSelectedModuleIdsChanged`, which still filters on the
**actor** gate only:

```csharp
		SelectedModuleIds = moduleIds
			.Where(moduleId => ModuleList.IsVisibleForAdministration(moduleId, _canViewAllModules))
			.Distinct()
			.ToArray();
```

That is deliberate. Filtering here on the role would drop a retained module the first time the admin
ticked anything else. `ToggleAllModules` was changed for the same reason — it used to be
`AllModulesSelected ? [] : ActiveVisibleModuleIds`, wiping the whole selection:

```csharp
	private void ToggleAllModules() =>
		OnSelectedModuleIdsChanged(AllModulesSelected
			? SelectedModuleIds.Except(ActiveVisibleModuleIds)
			: SelectedModuleIds.Concat(ActiveVisibleModuleIds));
```

**Hop 6 — chips.** `SelectedModuleChips` is gated by the actor, not the role, and tags each chip:

```csharp
	private IEnumerable<(ModuleDetailsDTO Module, bool IsOutsideRole)> SelectedModuleChips => Modules
		.Where(module => ModuleList.IsVisibleForAdministration(module.ModuleId, _canViewAllModules))
		.Where(module => SelectedModuleIds.Contains(module.ModuleId))
		.Select(module => (
			module,
			!ModuleList.IsInRoleModuleSet(module.ModuleId, User.RoleId)));
```

`AddUserComponent.razor:169`:

```razor
                                    @foreach (var (module, isOutsideRole) in SelectedModuleChips)
                                    {
                                        <span class="au-chip @(isOutsideRole ? "is-outside-role" : string.Empty)"
                                              title="@(isOutsideRole ? "Not available to the selected role" : null)">
```

Styled in `AddUserComponent.razor.css` using existing tokens only — no hex literals:

```css
.au-chip.is-outside-role {
    border-style: dashed;
    border-color: var(--au-border);
    background: var(--c-surface-sunken);
    color: var(--au-text-3);
}
```

**Hop 7 — submit.** `AddUserComponent.Submit()` validates, sets `User.ModuleIds = moduleIds`, and
closes with `DialogResult.Ok(User)`. Back in `UserManagement.razor`:

```csharp
            var addedUser = (AddATSUserDTO)result.Data!;
            var addResponse = await UserManagementService.AddUserAsync(addedUser);
```

`Submit` does **not** re-check the role set. A module outside it can therefore reach the API — that
is the intended consequence of "never strip silently", and the backend is the authority.

## 3. Edit User — the diff

Same shape, four differences:

- The target role is `EditUser.RoleId` (bound at `EditUserComponent.razor:164`), a private DTO built
  in `OnParametersSet` from the `UserManagementViewModel` parameter — not `User.RoleId`.
- `SelectedModuleIds` is seeded from the existing user: `SelectedModuleIds = User.ModuleIds.ToHashSet();`.
  This is where a role-disallowed module actually arrives already selected.
- `VisibleModules` is not `.Where(module => module.IsActive)`-filtered at the call site; instead each
  menu item carries `Disabled="@(!module.IsActive && !isSelected)"`, and chips append
  `@(!module.IsActive ? " (Inactive)" : string.Empty)`.
- `ToggleAllModules` was already additive (`Concat` / `Except`), so it needed no change.

The `.eu-chip.is-outside-role` rule in `EditUserComponent.razor.css` is a byte-for-byte analogue of
the `.au-` one with the `--eu-` token prefix. Both scoped sheets must change together; Blazor cannot
share one scoped stylesheet between two components.

## 4. Wiring nothing enforces at compile time

| Thing | Must independently agree with | Consequence of drift |
|---|---|---|
| `AllRoleModuleIds` + every `RoleOnlyModuleIds` entry | `ModuleList.List` keys | A module id in neither set is grantable by no role and silently vanishes from every dialog |
| `RestrictedAdministrationModuleIds` | Complement of the Client Admin set | An admin outside the Super Admin / Platform Manager ladder gains or loses a module. The test `IsVisibleForAdministration_ShouldMatchTheClientAdminRoleSet_WhenActorCannotViewAllModules` catches this |
| `AtsRoleList` constants | `ATSInitialData.GetATSRoles()` in the backend | A wrong id silently falls back to the shared eight, because `TryGetValue` misses |
| `RoleList.SuperAdminId` (platform) vs `AtsRoleList.PlatformManagerId` (ATS) | Nothing — both are `1` | The two are unrelated ladders that happen to share a value. Reading them as one is the easiest mistake here |
| `.au-chip.is-outside-role` / `.eu-chip.is-outside-role` | The `is-outside-role` literal in both `.razor` files | A typo loses the muted styling with no build error |

## 5. Change X, also check Y

| If you change | Also check |
|---|---|
| A module's role set | `AtsModuleListTests` — the matrix is asserted literally, in four places |
| `ModuleList.List` (add or remove a module) | `AllRoleModuleIds`, `RoleOnlyModuleIds`, `RestrictedAdministrationModuleIds`, and `IsPrimaryNavigationModule` |
| `AtsRoleList` | `BackendAPI/Modules/ATS/Data/DataSeed/ATSInitialData.cs` `GetATSRoles()`, and every `AtsRoleList.` reference in `Component/ATS` |
| The chip markup in either dialog | The other dialog, and both `.razor.css` files |
| `_canViewAllModules` derivation | `AddUserComponent`, `EditUserComponent`, **and** `AddModuleComponent.razor.cs`, which computes the same flag but still hardcodes `atsRoleId == 1` instead of `AtsRoleList.PlatformManagerId` |
| `OnSelectedModuleIdsChanged`'s filter | Hop 5 above — filtering on the role there reintroduces the silent strip |
