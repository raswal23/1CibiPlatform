using FrontendWebassembly.ShareData.ATS;

namespace FrontendWebassembly.Component.ATS;

public partial class EditUserComponent
{
	private const string ATSRoleIdStorageKey = "ATSRoleId";
	private MudForm? UserForm;
	private bool _canAssignAllRoles;
	private bool _canViewAllModules;

	[Inject]
	private IAccessService AccessService { get; set; } = default!;

	[Inject]
	private LocalStorageService LocalStorageService { get; set; } = default!;

	[CascadingParameter]
	private IMudDialogInstance? EditUserDialog { get; set; }

	[Parameter]
	public UserManagementViewModel User { get; set; } = new();

	[Parameter]
	public IReadOnlyList<ATSUserLookupDTO> AuthUsers { get; set; } = Array.Empty<ATSUserLookupDTO>();

	[Parameter]
	public IReadOnlyList<ClientDetailsDTO> Clients { get; set; } = Array.Empty<ClientDetailsDTO>();

	[Parameter]
	public IReadOnlyList<RoleDetailsDTO> Roles { get; set; } = Array.Empty<RoleDetailsDTO>();

	[Parameter]
	public IReadOnlyList<ModuleDetailsDTO> Modules { get; set; } = Array.Empty<ModuleDetailsDTO>();

	private EditATSUserDTO EditUser { get; set; } = new();
	private ATSUserLookupDTO? SelectedAuthUser { get; set; }
	private string? AuthUserError { get; set; }
	private IReadOnlyCollection<int> SelectedModuleIds { get; set; } = new HashSet<int>();
	private string? ModuleError { get; set; }
	// Restricted administration modules stay hidden from the menu and the chips
	// for admins outside the SuperAdmin / Platform Manager ladder, but any such
	// module already assigned to the user is kept in SelectedModuleIds so saving
	// never silently strips what this admin cannot see.
	private IEnumerable<ModuleDetailsDTO> VisibleModules => Modules
		.Where(module => ModuleList.IsVisibleForAdministration(module.ModuleId, _canViewAllModules));
	private IEnumerable<ModuleDetailsDTO> SelectedModules => VisibleModules
		.Where(module => SelectedModuleIds.Contains(module.ModuleId));
	private IReadOnlyCollection<int> ActiveModuleIds => VisibleModules
		.Where(module => module.IsActive)
		.Select(module => module.ModuleId)
		.ToArray();
	private bool AllModulesSelected => ActiveModuleIds.Count > 0
		&& ActiveModuleIds.All(SelectedModuleIds.Contains);
	private string SelectAllModulesIcon => AllModulesSelected
		? Icons.Material.Filled.CheckBox
		: SelectedModuleIds.Count > 0
			? Icons.Material.Filled.IndeterminateCheckBox
			: Icons.Material.Outlined.CheckBoxOutlineBlank;
	private IEnumerable<RoleDetailsDTO> AssignableRoles
	{
		get
		{
			if (_canAssignAllRoles)
				return Roles;

			return Roles.Where(role =>
				(role.RoleId != AtsRoleList.PlatformManagerId &&
				 role.RoleId != AtsRoleList.ServiceDeliveryId &&
				 role.RoleId != AtsRoleList.ClientExperienceId) ||
				role.RoleId == EditUser.RoleId);
		}
	}
	private string DisplayUserName => SelectedAuthUser?.UserName ?? User.UserName;
	private string DisplayUserEmail => SelectedAuthUser?.UserEmail ?? User.UserEmail;
	private string UserInitials
	{
		get
		{
			if (string.IsNullOrWhiteSpace(DisplayUserName))
				return "?";

			var nameParts = DisplayUserName.Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (nameParts.Length == 1)
				return nameParts[0][..Math.Min(2, nameParts[0].Length)].ToUpperInvariant();

			return $"{nameParts[0][0]}{nameParts[1][0]}".ToUpperInvariant();
		}
	}

	protected override async Task OnInitializedAsync()
	{
		var isPlatformSuperAdmin = await AccessService.HasRoleAsync(RoleList.SuperAdminId);
		var atsRoleId = await GetStoredATSRoleIdAsync();
		_canAssignAllRoles = isPlatformSuperAdmin || atsRoleId == AtsRoleList.PlatformManagerId;
		_canViewAllModules = isPlatformSuperAdmin || atsRoleId == AtsRoleList.PlatformManagerId;
	}

	private async Task<int> GetStoredATSRoleIdAsync()
	{
		try
		{
			return await LocalStorageService.GetItemAsync<int>(ATSRoleIdStorageKey);
		}
		catch (JsonException)
		{
			return 0;
		}
	}

	protected override void OnParametersSet()
	{
		SelectedAuthUser = AuthUsers.FirstOrDefault(authUser => authUser.UserId == User.UserId);
		AuthUserError = SelectedAuthUser is null
			? "This user is no longer available in the Auth ATS assignment list."
			: null;

		EditUser = new EditATSUserDTO
		{
			UserId = User.UserId,
			UserName = SelectedAuthUser?.UserName ?? string.Empty,
			UserEmail = SelectedAuthUser?.UserEmail ?? string.Empty,
			IsActive = User.IsActive,
			ClientId = User.ClientId,
			Site = User.Site,
			RoleId = User.RoleId
		};
		SelectedModuleIds = User.ModuleIds.ToHashSet();
	}

	private void Cancel() => EditUserDialog!.Cancel();

	private async Task Submit()
	{
		await UserForm!.ValidateAsync();
		var moduleIds = SelectedModuleIds.Distinct().ToHashSet();
		ModuleError = moduleIds.Count == 0 ? "At least one module is required" : null;

		if (UserForm.IsValid && AuthUserError is null && ModuleError is null)
		{
			EditUser.ModuleIds = moduleIds;
			EditUserDialog!.Close(DialogResult.Ok(EditUser));
		}
	}

	private Task<IEnumerable<ATSUserLookupDTO>> SearchAuthUsers(
		string value,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IEnumerable<ATSUserLookupDTO> users = AuthUsers;
		if (!string.IsNullOrWhiteSpace(value))
		{
			users = users.Where(user =>
				user.UserName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
				user.UserEmail.Contains(value, StringComparison.OrdinalIgnoreCase));
		}

		return Task.FromResult(users);
	}

	private static string GetAuthUserText(ATSUserLookupDTO? user)
	{
		return user is null ? string.Empty : $"{user.UserName} ({user.UserEmail})";
	}

	private void OnSelectedModuleIdsChanged(IEnumerable<int> moduleIds)
	{
		SelectedModuleIds = moduleIds.Distinct().ToArray();
		ModuleError = SelectedModuleIds.Count == 0 ? "At least one module is required" : null;
	}

	private void ToggleModule(int moduleId)
	{
		var moduleIds = SelectedModuleIds.ToHashSet();
		if (!moduleIds.Add(moduleId))
			moduleIds.Remove(moduleId);

		OnSelectedModuleIdsChanged(moduleIds);
	}

	// Selecting all only adds active modules; inactive ones already on the user
	// stay selected either way and are only removable through their chip.
	private void ToggleAllModules() =>
		OnSelectedModuleIdsChanged(AllModulesSelected
			? SelectedModuleIds.Except(ActiveModuleIds)
			: SelectedModuleIds.Concat(ActiveModuleIds));

	private void RemoveModule(int moduleId) =>
		OnSelectedModuleIdsChanged(SelectedModuleIds.Where(id => id != moduleId));

	private void ToggleStatus() => EditUser.IsActive = !EditUser.IsActive;
}
