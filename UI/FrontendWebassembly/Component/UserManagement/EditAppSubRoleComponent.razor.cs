namespace FrontendWebassembly.Component.UserManagement;

public partial class EditAppSubRoleComponent
{
	private MudForm? EditAppSubRoleForm;
	private bool IsLoaded;
	private UsersDTO? selectedUser;
	private ApplicationsDTO? selectedApp;
	private SubMenusDTO? selectedMenu;
	private RolesDTO? selectedRole;

	[CascadingParameter] private IMudDialogInstance? EditAppSubRoleDialog { get; set; }
	[Parameter] public AppSubRolesDTO AppSubRole { get; set; } = new();
	[Parameter] public IReadOnlyList<UsersDTO> Users { get; set; } = Array.Empty<UsersDTO>();
	[Parameter] public IReadOnlyList<ApplicationsDTO> Apps { get; set; } = Array.Empty<ApplicationsDTO>();
	[Parameter] public IReadOnlyList<SubMenusDTO> SubMenus { get; set; } = Array.Empty<SubMenusDTO>();
	[Parameter] public IReadOnlyList<RolesDTO> Roles { get; set; } = Array.Empty<RolesDTO>();

	private string DisplayUserName => selectedUser is null ? "User unavailable" : GetDisplayName(selectedUser);
	private string DisplayUserEmail => selectedUser?.email ?? AppSubRole.UserEmail ?? "No email available";
	private string UserInitials => GetInitials(DisplayUserName);

	protected override void OnParametersSet()
	{
		selectedUser = Users.FirstOrDefault(user => user.userId == AppSubRole.UserId);
		selectedApp = Apps.FirstOrDefault(app => app.applicationId == AppSubRole.AppId);
		selectedMenu = SubMenus.FirstOrDefault(subMenu => subMenu.subMenuId == AppSubRole.SubMenuId);
		selectedRole = Roles.FirstOrDefault(role => role.roleId == AppSubRole.RoleId);
		IsLoaded = selectedUser is not null && selectedApp is not null && selectedMenu is not null && selectedRole is not null;
	}

	private void Cancel() => EditAppSubRoleDialog!.Cancel();

	private async Task Submit()
	{
		await EditAppSubRoleForm!.ValidateAsync();
		if (!EditAppSubRoleForm.IsValid || !IsLoaded || selectedUser is null || selectedApp is null || selectedMenu is null || selectedRole is null)
			return;

		EditAppSubRoleDialog!.Close(DialogResult.Ok(new EditAppSubRoleDTO
		{
			AppSubRoleId = AppSubRole.AppRoleId,
			UserId = selectedUser.userId,
			AppId = selectedApp.applicationId,
			SubMenuId = selectedMenu.subMenuId,
			RoleId = selectedRole.roleId
		}));
	}

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
		if (string.IsNullOrWhiteSpace(name) || name == "User unavailable")
			return "?";

		var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return parts.Length == 1
			? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
			: $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
	}
}

// Walks a keyset cursor chain to materialize the full list for dialog pickers.
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
