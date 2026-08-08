namespace FrontendWebassembly.Component.ATS;

public partial class EditUserComponent
{
	private MudForm? UserForm;

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

	private string GetSelectedModulesText(IReadOnlyList<string> selectedValues)
	{
		var selectedIds = SelectedModuleIds.ToHashSet();
		return string.Join(", ", Modules
			.Where(module => selectedIds.Contains(module.ModuleId))
			.Select(module => module.ModuleName));
	}
}
