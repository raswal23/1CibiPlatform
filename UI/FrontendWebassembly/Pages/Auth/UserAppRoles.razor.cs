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
	private readonly CursorTableLoader<UnApprovedUsersDTO> _unapprovedUsersLoader = new();
	private readonly CursorTableLoader<LockedUsersDTO> _lockedUsersLoader = new();
	private readonly CursorTableLoader<UsersDTO> _usersLoader = new();
	private readonly CursorTableLoader<ApplicationsDTO> _applicationsLoader = new();
	private readonly CursorTableLoader<SubMenusDTO> _subMenusLoader = new();
	private readonly CursorTableLoader<RolesDTO> _rolesLoader = new();
	private readonly CursorTableLoader<AppSubRolesDTO> _appSubRolesLoader = new();
	private static DialogOptions UserManagementDialogOptions => new()
	{
		NoHeader = true,
		MaxWidth = MaxWidth.Small,
		FullWidth = true,
		BackdropClick = false
	};

	private string GetTabButtonClass(int index) =>
		_activeIndex == index ? "user-management-tab active" : "user-management-tab";

	private void SelectTab(int index) => _activeIndex = index;

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
		=> await LoadCursorPagedDataAsync(_usersLoader, state, $"{searchStringUser}", (cursor, pageSize) =>
			UserManagementService.GetUsersAsync(cursor, pageSize, searchStringUser));

	private async Task<TableData<UnApprovedUsersDTO>> LoadUnApprovedUsersServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadCursorPagedDataAsync(_unapprovedUsersLoader, state, $"{searchStringUnApprovedUser}", (cursor, pageSize) =>
			UserManagementService.GetUnApprovedUsersAsync(cursor, pageSize, searchStringUnApprovedUser));

	private async Task<TableData<LockedUsersDTO>> LoadLockedUsersServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadCursorPagedDataAsync(_lockedUsersLoader, state, $"{searchStringLockedUser}", (cursor, pageSize) =>
			UserManagementService.GetLockedUsersAsync(cursor, pageSize, searchStringLockedUser));

	private async Task<TableData<ApplicationsDTO>> LoadApplicationsServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadCursorPagedDataAsync(_applicationsLoader, state, $"{searchStringApp}", (cursor, pageSize) =>
			UserManagementService.GetApplicationsAsync(cursor, pageSize, searchStringApp));

	private async Task<TableData<SubMenusDTO>> LoadSubMenusServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadCursorPagedDataAsync(_subMenusLoader, state, $"{searchStringSub}", (cursor, pageSize) =>
			UserManagementService.GetSubMenusAsync(cursor, pageSize, searchStringSub));

	private async Task<TableData<RolesDTO>> LoadRolesServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadCursorPagedDataAsync(_rolesLoader, state, $"{searchStringRole}", (cursor, pageSize) =>
			UserManagementService.GetRolesAsync(cursor, pageSize, searchStringRole));

	private async Task<TableData<AppSubRolesDTO>> LoadUserAppSubRolesServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadCursorPagedDataAsync(_appSubRolesLoader, state, $"{searchStringAppSubRole}", (cursor, pageSize) =>
			UserManagementService.GetAppSubRolesAsync(cursor, pageSize, searchStringAppSubRole, cancellationToken));

	// Add and Edit Dialog
	private async Task OpenAddApplicationDialog()
	 => await OpenAddDialogAsync<AddApplicationComponent, AddApplicationDTO>("Add Application", AddApplication, UserManagementDialogOptions);

	private async Task OpenAddSubMenuDialog()
	 => await OpenAddDialogAsync<AddSubMenuComponent, AddSubMenuDTO>("Add SubMenu", AddSubMenu, UserManagementDialogOptions);

	private async Task OpenAddRoleDialog()
		=> await OpenAddDialogAsync<AddRoleComponent, AddRoleDTO>("Add Role", AddRole, UserManagementDialogOptions);

	private async Task OpenAddAppSubRoleDialog()
	{
		var dialog = await DialogService.ShowAsync<AddAppSubRoleComponent>(
			"Add User's AppSubRole",
			UserManagementDialogOptions);
		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not AddAppSubRoleResult addResult)
			return;

		var response = await UserManagementService.AddAppSubRoleAsync(addResult.AppSubRole);
		if (!response.IsSuccess || !response.Data)
		{
			if (!response.IsSuccess)
				Snackbar.Add(response.ErrorDetail, Severity.Error);
			return;
		}

		Snackbar.Add("Application role assigned successfully", Severity.Success);
		if (appSubRolesTable?.TableRef is not null)
			await appSubRolesTable.TableRef.ReloadServerData();

		var notificationResponse = await UserManagementService.SendNotificationAsync(addResult.Notification);
		if (!notificationResponse.IsSuccess)
			Snackbar.Add("Saved, but the notification could not be sent.", Severity.Warning);
	}

	private async Task OpenEditUserApprovalDialog(UnApprovedUsersDTO unapproveduser)
	{
		await OpenEditDialogAsync<EditUserApprovalComponent, UnApprovedUsersDTO>("User Approval", "User", unapproveduser, async result =>
		{
			await EditUser(result);

			var notificationResponse = await UserManagementService.SendApprovalNotificationAsync(result.email!);

			if (!notificationResponse.IsSuccess)
			{
				Snackbar.Add("Saved, but the approval notification could not be sent.", Severity.Warning);
			}
		}, UserManagementDialogOptions);
	}
	private async Task OpenEditApplicationDialog(ApplicationsDTO app)
	  => await OpenEditDialogAsync<EditApplicationComponent, ApplicationsDTO>("Edit Application", "Application", app, EditApplication, UserManagementDialogOptions);

	private async Task OpenEditSubMenuDialog(SubMenusDTO sub)
		=> await OpenEditDialogAsync<EditSubMenuComponent, SubMenusDTO>("Edit SubMenu", "SubMenu", sub, EditSubMenu, UserManagementDialogOptions);

	private async Task OpenEditRoleDialog(RolesDTO role)
		=> await OpenEditDialogAsync<EditRoleComponent, RolesDTO>("Edit Role", "Role", role, EditRole, UserManagementDialogOptions);

	private async Task OpenEditAppSubRoleDialog(AppSubRolesDTO appSubRole)
	{
		var parameters = new DialogParameters<EditAppSubRoleComponent>
		{
			{ component => component.AppSubRole, appSubRole }
		};
		var dialog = await DialogService.ShowAsync<EditAppSubRoleComponent>(
			"Edit User's AppSubRole",
			parameters,
			UserManagementDialogOptions);
		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not EditAppSubRoleDTO editAppSubRole)
			return;

		var response = await UserManagementService.EditAppSubRoleAsync(editAppSubRole);
		if (!response.IsSuccess)
		{
			Snackbar.Add(response.ErrorDetail, Severity.Error);
			return;
		}

		Snackbar.Add("Application role updated successfully", Severity.Success);
		if (appSubRolesTable?.TableRef is not null)
			await appSubRolesTable.TableRef.ReloadServerData();
	}

	// Delete Dialog
	private async Task ConfirmDelete(int id, string table)
	{
		var confirmed = await ShowUserManagementConfirmationAsync(
			"Confirm Delete",
			$"Are you sure you want to delete this {table}?",
			"Delete");

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
		var confirmed = await ShowUserManagementConfirmationAsync(
			"Unlocking User Account",
			"Are you sure you want to unlock this account?",
			"Unlock");

		if (confirmed)
		{
			await DeleteLockedUser(id);
		}
	}

	private async Task<bool> ShowUserManagementConfirmationAsync(string title, string message, string confirmText)
	{
		var parameters = new DialogParameters<ConfirmationDialogComponent>
		{
			{ component => component.Title, title },
			{ component => component.Message, message },
			{ component => component.ConfirmText, confirmText }
		};

		var dialog = await DialogService.ShowAsync<ConfirmationDialogComponent>(
			title,
			parameters,
			UserManagementDialogOptions);
		var result = await dialog.Result;

		return result is not null && !result.Canceled;
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

}
