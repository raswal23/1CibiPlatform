using FrontendWebassembly.Component.UserManagement;

namespace FrontendWebassembly.Pages.Auth;

public partial class UserAppRoles
{
	private int _activeIndex = 0;
	private string _searchStringUnApprovedUser;
	private string _searchStringLockedUser;
	private string _searchStringUser;
	private string _searchStringApp;
	private string _searchStringSub;
	private string _searchStringRole;
	private string _searchStringAppSubRole;
	private TableComponent<UnApprovedUsersDTO> unapprovedUsersTable;
	private TableComponent<LockedUsersDTO> lockedUsersTable;
	private TableComponent<UsersDTO> usersTable;
	private TableComponent<ApplicationsDTO> applicationsTable;
	private TableComponent<SubMenusDTO> subMenusTable;
	private TableComponent<RolesDTO> rolesTable;
	private TableComponent<AppSubRolesDTO> appSubRolesTable;

	private void OnTabChanged(int index)
	{
		_activeIndex = index;
	}

	private string GetTabClass(int index)
	{
		return _activeIndex == index
			? "philsys-tab-pannel-active"
			: "philsys-tab-pannel-inactive";
	}

	// Generic Functions
	private void UpdateSearch<T>(ref string field, string value, TableComponent<T> table) where T : class
	{
		if (field != value)
		{
			field = value;
			table?.TableRef.ReloadServerData();
		}
	}

	// Search 
	private string searchStringUnApprovedUser
	{
		get => _searchStringUnApprovedUser;
		set => UpdateSearch(ref _searchStringUnApprovedUser, value, unapprovedUsersTable);
	}

	private string searchStringLockedUser
	{
		get => _searchStringLockedUser;
		set => UpdateSearch(ref _searchStringLockedUser, value, lockedUsersTable);
	}

	private string searchStringUser
	{
		get => _searchStringUser;
		set => UpdateSearch(ref _searchStringUser, value, usersTable);
	}

	private string searchStringApp
	{
		get => _searchStringApp;
		set => UpdateSearch(ref _searchStringApp, value, applicationsTable);
	}

	private string searchStringSub
	{
		get => _searchStringSub;
		set => UpdateSearch(ref _searchStringSub, value, subMenusTable);
	}

	private string searchStringRole
	{
		get => _searchStringRole;
		set => UpdateSearch(ref _searchStringRole, value, rolesTable);
	}

	private string searchStringAppSubRole
	{
		get => _searchStringAppSubRole;
		set => UpdateSearch(ref _searchStringAppSubRole, value, appSubRolesTable);
	}

	// Load Tables
	private async Task<TableData<UsersDTO>> LoadUsersServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetUsersAsync(page, pageSize, searchStringUser));

	private async Task<TableData<UnApprovedUsersDTO>> LoadUnApprovedUsersServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetUnApprovedUsersAsync(page, pageSize, searchStringUnApprovedUser));

	private async Task<TableData<LockedUsersDTO>> LoadLockedUsersServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetLockedUsersAsync(page, pageSize, searchStringLockedUser));

	private async Task<TableData<ApplicationsDTO>> LoadApplicationsServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetApplicationsAsync(page, pageSize, searchStringApp));

	private async Task<TableData<SubMenusDTO>> LoadSubMenusServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetSubMenusAsync(page, pageSize, searchStringSub));

	private async Task<TableData<RolesDTO>> LoadRolesServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetRolesAsync(page, pageSize, searchStringRole));

	private async Task<TableData<AppSubRolesDTO>> LoadUserAppSubRolesServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			UserManagementService.GetAppSubRolesAsync(page, pageSize, searchStringAppSubRole));

	// Add and Edit Dialog
	private async Task OpenAddApplicationDialog()
	 => await OpenAddDialogAsync<AddApplicationComponent, AddApplicationDTO>("Add Application", AddApplication);

	private async Task OpenAddSubMenuDialog()
	 => await OpenAddDialogAsync<AddSubMenuComponent, AddSubMenuDTO>("Add SubMenu", AddSubMenu);

	private async Task OpenAddRoleDialog()
		=> await OpenAddDialogAsync<AddRoleComponent, AddRoleDTO>("Add Role", AddRole);

	private async Task OpenAddAppSubRoleDialog()
	{
		await OpenAddDialogAsync<AddAppSubRoleComponent, AddAppSubRoleResult>(
			"Add User's AppSubRole",
			async result =>
			{
				var appSubRoleDto = result.AppSubRole;
				var notificationDto = result.Notification;
				var success = await AddAppSubRole(appSubRoleDto);
				if (!success)
				{
					return;
				}

				await UserManagementService.SendNotificationAsync(notificationDto);

			});
	}

	private async Task OpenEditUserApprovalDialog(UnApprovedUsersDTO unapproveduser)
	{
		await OpenEditDialogAsync<EditUserApprovalComponent, UnApprovedUsersDTO>("User Approval", "User", unapproveduser, async result =>
		{
			await EditUser(result);
			await UserManagementService.SendApprovalNotificationAsync(result.email!);
		});
	}
	private async Task OpenEditApplicationDialog(ApplicationsDTO app)
	  => await OpenEditDialogAsync<EditApplicationComponent, ApplicationsDTO>("Edit Application", "Application", app, EditApplication);

	private async Task OpenEditSubMenuDialog(SubMenusDTO sub)
		=> await OpenEditDialogAsync<EditSubMenuComponent, SubMenusDTO>("Edit SubMenu", "SubMenu", sub, EditSubMenu);

	private async Task OpenEditRoleDialog(RolesDTO role)
		=> await OpenEditDialogAsync<EditRoleComponent, RolesDTO>("Edit Role", "Role", role, EditRole);

	private async Task OpenEditAppSubRoleDialog(AppSubRolesDTO appsubrole)
		=> await OpenEditDialogAsync<EditAppSubRoleComponent, AppSubRolesDTO>("Edit UserAppSubRole", "AppSubRole", appsubrole, EditAppSubRole);

	// Delete Dialog
	private async Task ConfirmDelete(int id, string table)
	{
		var confirmed = await ConfirmActionAsync("Confirm Delete", $"Are you sure you want to delete this {table}?", "Delete");

		if (confirmed)
		{
			switch (table)
			{
				case "application":
					await DeleteApplication(id);
					break;
				case "submenu":
					await DeleteSubMenu(id);
					break;
				case "role":
					await DeleteRole(id);
					break;
				case "appsubrole":
					await DeleteAppSubRole(id);
					break;
			}
		}
	}

	private async Task ConfirmUnlockAccount(Guid id)
	{
		var confirmed = await ConfirmActionAsync("Unlocking User Account", "Are you sure you want to unlock this account?", "Unlock");

		if (confirmed)
		{
			await DeleteLockedUser(id);
		}
	}

	// Command Execution 
	private async Task DeleteApplication(int AppId)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.DeleteApplicationAsync(AppId), applicationsTable);
		return;
	}

	private async Task DeleteSubMenu(int SubMenuId)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.DeleteSubMenuAsync(SubMenuId), subMenusTable);
		return;
	}

	private async Task DeleteRole(int RoleId)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.DeleteRoleAsync(RoleId), rolesTable);
		return;
	}

	private async Task DeleteAppSubRole(int AppSubRoleId)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.DeleteUserAppSubRoleAsync(AppSubRoleId), appSubRolesTable);
		return;
	}

	private async Task DeleteLockedUser(Guid lockedUserId)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.DeleteLockedUserAsync(lockedUserId), lockedUsersTable);
		return;
	}

	private async Task AddApplication(AddApplicationDTO addApplicationDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.AddApplicationAsync(addApplicationDTO), applicationsTable);
		return;
	}

	private async Task AddSubMenu(AddSubMenuDTO addSubMenuDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.AddSubMenuAsync(addSubMenuDTO), subMenusTable);
		return;
	}

	private async Task AddRole(AddRoleDTO addRoleDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.AddRoleAsync(addRoleDTO), rolesTable);
		return;
	}

	private async Task<bool> AddAppSubRole(AddAppSubRoleDTO addAppSubRoleDTO)
	{
		var isSuccess = await UserManagementService.AddAppSubRoleAsync(addAppSubRoleDTO);
		if (!isSuccess)
		{
			return false;
		}
		await ExecuteAndReloadAsync(() => Task.CompletedTask, appSubRolesTable);
		return true;
	}

	private async Task EditUser(UnApprovedUsersDTO editUserDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.EditUserAsync(editUserDTO), unapprovedUsersTable);
		StateHasChanged();
		return;
	}

	private async Task EditApplication(ApplicationsDTO editApplicationDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.EditApplicationAsync(editApplicationDTO), applicationsTable);
		StateHasChanged();
		return;
	}

	private async Task EditSubMenu(SubMenusDTO editSubMenuDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.EditSubMenuAsync(editSubMenuDTO), subMenusTable);
		return;
	}

	private async Task EditRole(RolesDTO editRoleDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.EditRoleAsync(editRoleDTO), rolesTable);
		return;
	}

	private async Task EditAppSubRole(AppSubRolesDTO editAppSubRoleDTO)
	{
		await ExecuteAndReloadAsync(() => UserManagementService.EditAppSubRoleAsync(editAppSubRoleDTO), appSubRolesTable);
		return;
	}
}
