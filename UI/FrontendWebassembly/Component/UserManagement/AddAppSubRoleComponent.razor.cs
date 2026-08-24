namespace FrontendWebassembly.Component.UserManagement;

public partial class AddAppSubRoleComponent
{
	private MudForm? AddAppSubRoleForm;
	private UsersDTO? selectedUser;
	private ApplicationsDTO? selectedApp;
	private SubMenusDTO? selectedMenu;
	private RolesDTO? selectedRole;

	[CascadingParameter] private IMudDialogInstance? AddAppSubRoleDialog { get; set; }
	[Parameter] public AddAppSubRoleDTO AppSubRole { get; set; } = new();
	[Parameter] public IReadOnlyList<UsersDTO> Users { get; set; } = Array.Empty<UsersDTO>();
	[Parameter] public IReadOnlyList<ApplicationsDTO> Apps { get; set; } = Array.Empty<ApplicationsDTO>();
	[Parameter] public IReadOnlyList<SubMenusDTO> SubMenus { get; set; } = Array.Empty<SubMenusDTO>();
	[Parameter] public IReadOnlyList<RolesDTO> Roles { get; set; } = Array.Empty<RolesDTO>();

	private string DisplayUserName => selectedUser is null ? "Select a user" : GetDisplayName(selectedUser);
	private string DisplayUserEmail => selectedUser?.email ?? "Their email will appear here";
	private string UserInitials => GetInitials(DisplayUserName);

	protected override async Task OnInitializedAsync()
	{
		AppSubRole.AssignedBy = await LocalStorageService.GetItemAsync<Guid>("UserId");
	}

	private void Cancel() => AddAppSubRoleDialog!.Cancel();

	private async Task Submit()
	{
		await AddAppSubRoleForm!.ValidateAsync();
		if (!AddAppSubRoleForm.IsValid || selectedUser is null || selectedApp is null || selectedMenu is null || selectedRole is null)
			return;

		var notification = new AssignmentNotificationDTO
		{
			Gmail = selectedUser.email,
			Application = selectedApp.applicationName,
			SubMenu = selectedMenu.subMenuName,
			Role = selectedRole.roleName
		};

		AppSubRole.UserId = selectedUser.userId;
		AppSubRole.AppId = selectedApp.applicationId;
		AppSubRole.SubMenuId = selectedMenu.subMenuId;
		AppSubRole.RoleId = selectedRole.roleId;

		AddAppSubRoleDialog!.Close(DialogResult.Ok(new AddAppSubRoleResult(AppSubRole, notification)));
	}

	private void OnSelectedUserChanged(UsersDTO? user) => selectedUser = user;

	private Task<IEnumerable<T>> Search<T>(
		string value,
		IEnumerable<T> source,
		Func<T, string?> selector,
		CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(value))
			return Task.FromResult(source);

		return Task.FromResult(source.Where(item =>
			(selector(item) ?? string.Empty).Contains(value, StringComparison.OrdinalIgnoreCase)));
	}

	private Task<IEnumerable<UsersDTO>> SearchUsers(string value, CancellationToken token) =>
		Search(value, Users, user => $"{user.firstName} {user.middleName} {user.lastName} {user.email}", token);

	private Task<IEnumerable<ApplicationsDTO>> SearchApplications(string value, CancellationToken token) =>
		Search(value, Apps, app => app.applicationName, token);

	private Task<IEnumerable<SubMenusDTO>> SearchSubMenus(string value, CancellationToken token) =>
		Search(value, SubMenus, subMenu => subMenu.subMenuName, token);

	private Task<IEnumerable<RolesDTO>> SearchRoles(string value, CancellationToken token) =>
		Search(value, Roles, role => role.roleName, token);

	private static string GetUserText(UsersDTO? user) => user is null
		? string.Empty
		: $"{GetDisplayName(user)} ({user.email})";

	private static string GetDisplayName(UsersDTO user)
	{
		var name = string.Join(" ", new[] { user.firstName, user.middleName, user.lastName }
			.Where(part => !string.IsNullOrWhiteSpace(part)));
		return string.IsNullOrWhiteSpace(name) ? user.email ?? "Unknown user" : name;
	}

	private static string GetInitials(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name == "Select a user")
			return "?";

		var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return parts.Length == 1
			? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
			: $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
	}
}
