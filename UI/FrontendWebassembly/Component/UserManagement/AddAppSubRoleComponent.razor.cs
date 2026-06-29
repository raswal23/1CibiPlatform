namespace FrontendWebassembly.Component.UserManagement;

public partial class AddAppSubRoleComponent
{
	private MudForm? AddAppSubRoleForm;
	private bool showUserError;
	private bool showAppError;
	private bool showSubMenuError;
	private bool showRoleError;

	private UsersDTO? selectedUser;
	private ApplicationsDTO? selectedApp;
	private SubMenusDTO? selectedMenu;
	private RolesDTO? selectedRole;

	[CascadingParameter] IMudDialogInstance? AddAppSubRoleDialog { get; set; }
	[Parameter] public AddAppSubRoleDTO AppSubRole { get; set; } = new AddAppSubRoleDTO();

	private List<UsersDTO>? Users = new();
	private List<ApplicationsDTO>? Apps = new();
	private List<SubMenusDTO>? SubMenus = new();
	private List<RolesDTO>? Roles = new();

	void Cancel() => AddAppSubRoleDialog!.Cancel();

	protected override async Task OnInitializedAsync()
	{
		AppSubRole.AssignedBy = await LocalStorageService.GetItemAsync<Guid>("UserId");

		Users = (await UserManagementService.GetUsersAsync(1, int.MaxValue)).Data.ToList();
		Apps = (await UserManagementService.GetApplicationsAsync(1, int.MaxValue)).Data.ToList();
		SubMenus = (await UserManagementService.GetSubMenusAsync(1, int.MaxValue)).Data.ToList();
		Roles = (await UserManagementService.GetRolesAsync(1, int.MaxValue)).Data.ToList();
	}

	async Task Submit()
	{
		var notification = new AssignmentNotificationDTO
		{
			Gmail = selectedUser?.email,
			Application = selectedApp?.applicationName,
			SubMenu = selectedMenu?.subMenuName,
			Role = selectedRole?.roleName
		};

		AppSubRole.UserId = selectedUser?.userId ?? Guid.Empty;
		AppSubRole.AppId = selectedApp?.applicationId ?? 0;
		AppSubRole.SubMenuId = selectedMenu?.subMenuId ?? 0;
		AppSubRole.RoleId = selectedRole?.roleId ?? 0;

		AddAppSubRoleDialog!.Close(DialogResult.Ok(new AddAppSubRoleResult(AppSubRole, notification)));
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
