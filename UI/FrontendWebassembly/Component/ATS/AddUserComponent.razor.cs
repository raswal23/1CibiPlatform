namespace FrontendWebassembly.Component.ATS;

public partial class AddUserComponent
{
	private const string SuperAdminEmail = "admin@cibi.com";
	private MudForm? UserForm;

	[CascadingParameter]
	private IMudDialogInstance? AddUserDialog { get; set; }

	[Parameter]
	public AddATSUserDTO User { get; set; } = new();

	[Parameter]
	public IReadOnlyList<ATSUserLookupDTO> AuthUsers { get; set; } = Array.Empty<ATSUserLookupDTO>();

	[Parameter]
	public IReadOnlyList<RoleDetailsDTO> Roles { get; set; } = Array.Empty<RoleDetailsDTO>();

	[Parameter]
	public IReadOnlyList<ModuleDetailsDTO> Modules { get; set; } = Array.Empty<ModuleDetailsDTO>();

	[Parameter]
	public IReadOnlyList<UserClientDetailsDTO> Assignments { get; set; } = Array.Empty<UserClientDetailsDTO>();

	private IReadOnlyCollection<int> SelectedModuleIds { get; set; } = new HashSet<int>();
	private ATSUserLookupDTO? SelectedAuthUser { get; set; }
	private string? AuthUserError { get; set; }
	private string? ModuleError { get; set; }
	private IEnumerable<ModuleDetailsDTO> SelectedModules => Modules
		.Where(module => SelectedModuleIds.Contains(module.ModuleId));
	private string UserInitials
	{
		get
		{
			if (string.IsNullOrWhiteSpace(User.UserName))
				return "?";

			var nameParts = User.UserName.Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (nameParts.Length == 1)
				return nameParts[0][..Math.Min(2, nameParts[0].Length)].ToUpperInvariant();

			return $"{nameParts[0][0]}{nameParts[1][0]}".ToUpperInvariant();
		}
	}

	private void Cancel() => AddUserDialog!.Cancel();

	private async Task Submit()
	{
		await UserForm!.ValidateAsync();
		AuthUserError = SelectedAuthUser is null || User.UserId == Guid.Empty
			? "User is required"
			: RequiresClientAssignment && User.ClientId is not > 0
				? "Assign a client before configuring ATS access"
				: null;
		var moduleIds = SelectedModuleIds.Distinct().ToHashSet();
		ModuleError = moduleIds.Count == 0 ? "At least one module is required" : null;

		if (UserForm.IsValid &&
			AuthUserError is null &&
			ModuleError is null)
		{
			User.ModuleIds = moduleIds;
			AddUserDialog!.Close(DialogResult.Ok(User));
		}
	}

	private void OnAuthUserChanged(ATSUserLookupDTO? authUser)
	{
		SelectedAuthUser = authUser;
		User.UserId = authUser?.UserId ?? Guid.Empty;
		User.UserName = authUser?.UserName ?? string.Empty;
		User.UserEmail = authUser?.UserEmail ?? string.Empty;
		User.ClientId = authUser is null
			? null
			: Assignments.FirstOrDefault(item => item.UserId == authUser.UserId)?.ClientId;
		AuthUserError = authUser is null
			? "User is required"
			: RequiresClientAssignment && User.ClientId is not > 0
				? "Assign a client before configuring ATS access"
				: null;
	}

	private bool RequiresClientAssignment =>
		SelectedAuthUser is not null &&
		!string.Equals(SelectedAuthUser.UserEmail, SuperAdminEmail, StringComparison.OrdinalIgnoreCase);

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

	private void RemoveModule(int moduleId) =>
		OnSelectedModuleIdsChanged(SelectedModuleIds.Where(id => id != moduleId));

	private void ToggleStatus() => User.IsActive = !User.IsActive;
}
