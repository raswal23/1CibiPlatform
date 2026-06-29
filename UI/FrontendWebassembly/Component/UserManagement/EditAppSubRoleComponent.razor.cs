namespace FrontendWebassembly.Component.UserManagement;

public partial class EditAppSubRoleComponent
{
	private MudForm? EditAppSubRoleForm;
	private bool IsLoaded = false;
	private UsersDTO? selectedUser;
	private ApplicationsDTO? selectedApp;
	private SubMenusDTO? selectedMenu;
	private RolesDTO? selectedRole;
	[CascadingParameter] IMudDialogInstance? EditAppSubRoleDialog { get; set; }

	[Parameter]
	public AppSubRolesDTO AppSubRole { get; set; } = new AppSubRolesDTO();

	private List<UsersDTO>? Users = new();
	private List<ApplicationsDTO>? Apps = new();
	private List<SubMenusDTO>? SubMenus = new();
	private List<RolesDTO>? Roles = new();

	void Cancel() => EditAppSubRoleDialog!.Cancel();

	protected override async Task OnInitializedAsync()
	{
		Users = (await UserManagementService.GetUsersAsync(1, int.MaxValue)).Data.ToList();
		Apps = (await UserManagementService.GetApplicationsAsync(1, int.MaxValue)).Data.ToList();
		SubMenus = (await UserManagementService.GetSubMenusAsync(1, int.MaxValue)).Data.ToList();
		Roles = (await UserManagementService.GetRolesAsync(1, int.MaxValue)).Data.ToList();

		selectedUser = Users?.FirstOrDefault(u => u.email == AppSubRole.UserEmail);
		selectedApp = Apps?.FirstOrDefault(a => a.applicationName == AppSubRole.AppName);
		selectedMenu = SubMenus?.FirstOrDefault(s => s.subMenuName == AppSubRole.SubMenuName);
		selectedRole = Roles?.FirstOrDefault(r => r.roleName == AppSubRole.RoleName);

		IsLoaded = true;
	}

	async Task Submit()
	{
		AppSubRole.UserId = selectedUser!.userId;
		AppSubRole.AppId = selectedApp!.applicationId;
		AppSubRole.SubMenuId = selectedMenu!.subMenuId;
		AppSubRole.RoleId = selectedRole!.roleId;

		EditAppSubRoleDialog!.Close(DialogResult.Ok(AppSubRole));
	}

	private async Task<IEnumerable<T>> Search<T>(
	string value,
	IEnumerable<T> source,
	Func<T, string?> selector,
	CancellationToken token)
	{
		await Task.Delay(300, token);

		if (string.IsNullOrWhiteSpace(value))
			return source;

		return source.Where(x =>
			(selector(x) ?? string.Empty)
			.Contains(value, StringComparison.OrdinalIgnoreCase));
	}

	private Task<IEnumerable<UsersDTO>> SearchUsers(string value, CancellationToken token)
	=> Search(value, Users!, u => u.email, token);

	private Task<IEnumerable<ApplicationsDTO>> SearchApplications(string value, CancellationToken token)
	=> Search(value, Apps!, a => a.applicationName, token);

	private Task<IEnumerable<SubMenusDTO>> SearchSubMenus(string value, CancellationToken token)
	=> Search(value, SubMenus!, s => s.subMenuName, token);

	private Task<IEnumerable<RolesDTO>> SearchRoles(string value, CancellationToken token)
	=> Search(value, Roles!, r => r.roleName, token);
}
