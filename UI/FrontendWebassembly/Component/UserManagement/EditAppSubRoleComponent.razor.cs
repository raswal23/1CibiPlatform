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
		var (users, usersError) = await KeysetPageWalker.FetchAllPagesAsync((cursor, pageSize) => UserManagementService.GetUsersAsync(cursor, pageSize));
		var (apps, appsError) = await KeysetPageWalker.FetchAllPagesAsync((cursor, pageSize) => UserManagementService.GetApplicationsAsync(cursor, pageSize));
		var (subMenus, subMenusError) = await KeysetPageWalker.FetchAllPagesAsync((cursor, pageSize) => UserManagementService.GetSubMenusAsync(cursor, pageSize));
		var (roles, rolesError) = await KeysetPageWalker.FetchAllPagesAsync((cursor, pageSize) => UserManagementService.GetRolesAsync(cursor, pageSize));

		var firstFailure = new[] { usersError, appsError, subMenusError, rolesError }
			.FirstOrDefault(error => !string.IsNullOrEmpty(error));

		if (firstFailure is not null)
		{
			Snackbar.Add(firstFailure, Severity.Error);
		}

		Users = users;
		Apps = apps;
		SubMenus = subMenus;
		Roles = roles;

		selectedUser = Users?.FirstOrDefault(u => u.email == AppSubRole.UserEmail);
		selectedApp = Apps?.FirstOrDefault(a => a.applicationId == AppSubRole.AppId);
		selectedMenu = SubMenus?.FirstOrDefault(s => s.subMenuId == AppSubRole.SubMenuId);
		selectedRole = Roles?.FirstOrDefault(r => r.roleId == AppSubRole.RoleId);

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

// Walks a keyset cursor chain to materialize the full list for dialog pickers,
// replacing the old "page 1 with int.MaxValue page size" pattern (the backend
// now clamps page size, so a single request cannot fetch everything).
internal static class KeysetPageWalker
{
	internal static async Task<(List<TItem> Items, string? Error)> FetchAllPagesAsync<TItem>(
		Func<string?, int, Task<ServiceResponse<KeysetPaginatedResult<TItem>>>> fetchPage)
		where TItem : class
	{
		const int pageSize = 100;
		var items = new List<TItem>();
		string? cursor = null;

		while (true)
		{
			var response = await fetchPage(cursor, pageSize);

			if (!response.IsSuccess || response.Data is null)
				return (items, response.ErrorDetail);

			items.AddRange(response.Data.Items);

			if (response.Data.NextCursor is null || response.Data.Items.Count == 0)
				return (items, null);

			cursor = response.Data.NextCursor;
		}
	}
}
