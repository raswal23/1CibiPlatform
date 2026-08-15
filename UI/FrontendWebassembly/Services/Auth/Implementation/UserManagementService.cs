namespace FrontendWebassembly.Services.Auth.Implementation;

public class UserManagementService : IUserManagementService
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<UserManagementService> _logger;

	public UserManagementService(IHttpClientFactory httpClientFactory, ILogger<UserManagementService> logger)
	{
		_httpClient = httpClientFactory.CreateClient("API");
		_logger = logger;
	}

	public Task<ServiceResponse<PaginatedResult<UsersDTO>>> GetUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default)
		=> GetPagedAsync<UsersResponseDTO, UsersDTO>(
			BuildPagedQuery("auth/getusers", PageNumber, PageSize, SearchTerm),
			envelope => envelope.users,
			ct);

	public Task<ServiceResponse<PaginatedResult<UnApprovedUsersDTO>>> GetUnApprovedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default)
		=> GetPagedAsync<UnApprovedUsersResponseDTO, UnApprovedUsersDTO>(
			BuildPagedQuery("auth/getunapprovedusers", PageNumber, PageSize, SearchTerm),
			envelope => envelope.users,
			ct);

	public Task<ServiceResponse<PaginatedResult<ApplicationsDTO>>> GetApplicationsAsync(int? PageNumber = 1, int? PageSize = int.MaxValue, string? SearchTerm = null, CancellationToken ct = default)
		=> GetPagedAsync<ApplicationsResponseDTO, ApplicationsDTO>(
			BuildPagedQuery("auth/getapplications", PageNumber, PageSize, SearchTerm),
			envelope => envelope.applications,
			ct);

	public Task<ServiceResponse<PaginatedResult<SubMenusDTO>>> GetSubMenusAsync(
		int? PageNumber = 1,
		int? PageSize = 10,
		string? SearchTerm = null,
		CancellationToken ct = default)
		=> GetPagedAsync<SubMenusResponseDTO, SubMenusDTO>(
			BuildPagedQuery("auth/getsubmenus", PageNumber, PageSize, SearchTerm),
			envelope => envelope.submenus,
			ct);

	public Task<ServiceResponse<PaginatedResult<LockedUsersDTO>>> GetLockedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default)
		=> GetPagedAsync<LockedUsersResponseDTO, LockedUsersDTO>(
			BuildPagedQuery("auth/getlockedusers", PageNumber, PageSize, SearchTerm),
			envelope => envelope.lockedusers,
			ct);

	public Task<ServiceResponse<PaginatedResult<RolesDTO>>> GetRolesAsync(
		int? PageNumber = 1,
		int? PageSize = 10,
		string? SearchTerm = null,
		CancellationToken ct = default)
		=> GetPagedAsync<RolesResponseDTO, RolesDTO>(
			BuildPagedQuery("auth/getroles", PageNumber, PageSize, SearchTerm),
			envelope => envelope.roles,
			ct);

	public Task<ServiceResponse<PaginatedResult<AppSubRolesDTO>>> GetAppSubRolesAsync(
		int? PageNumber = 1,
		int? PageSize = 10,
		string? SearchTerm = null,
		CancellationToken ct = default)
		=> GetPagedAsync<AppSubRolesResponseDTO, AppSubRolesDTO>(
			BuildPagedQuery("auth/getappsubroles", PageNumber, PageSize, SearchTerm),
			envelope => envelope.appsubroles,
			ct);

	public Task<ServiceResponse<bool>> DeleteApplicationAsync(int AppId)
		=> SendForBoolAsync(() => _httpClient.DeleteAsync($"auth/deleteapplication/{AppId}"));

	public Task<ServiceResponse<bool>> DeleteSubMenuAsync(int SubMenuId)
		=> SendForBoolAsync(() => _httpClient.DeleteAsync($"auth/deletesubmenu/{SubMenuId}"));

	public Task<ServiceResponse<bool>> DeleteRoleAsync(int RoleId)
		=> SendForBoolAsync(() => _httpClient.DeleteAsync($"auth/deleterole/{RoleId}"));

	public Task<ServiceResponse<bool>> DeleteUserAppSubRoleAsync(int AppSubRoleId)
		=> SendForBoolAsync(() => _httpClient.DeleteAsync($"auth/deleteappsubrole/{AppSubRoleId}"));

	public Task<ServiceResponse<bool>> DeleteLockedUserAsync(Guid lockedUserId)
		=> SendForBoolAsync(() => _httpClient.DeleteAsync($"auth/deletelockeduser/{lockedUserId}"));

	public Task<ServiceResponse<bool>> AddApplicationAsync(AddApplicationDTO application)
		=> SendForBoolAsync(() => _httpClient.PostAsJsonAsync($"auth/addapplication", new { application }));

	public Task<ServiceResponse<bool>> AddSubMenuAsync(AddSubMenuDTO subMenu)
		=> SendForBoolAsync(() => _httpClient.PostAsJsonAsync($"auth/addsubmenu", new { subMenu }));

	public Task<ServiceResponse<bool>> AddRoleAsync(AddRoleDTO role)
		=> SendForBoolAsync(() => _httpClient.PostAsJsonAsync($"auth/addrole", new { role }));

	public Task<ServiceResponse<bool>> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
		=> SendForBoolAsync(() => _httpClient.PostAsJsonAsync($"auth/addappsubrole", new { appSubRole }));

	public Task<ServiceResponse<bool>> SendNotificationAsync(AssignmentNotificationDTO accountNotificationDTO)
		=> SendForBoolAsync(() => _httpClient.PostAsJsonAsync("account/notification", new { accountNotificationDTO }));

	public Task<ServiceResponse<bool>> SendApprovalNotificationAsync(string Gmail)
		=> SendForBoolAsync(() => _httpClient.PostAsJsonAsync("account/approvalnotification", new { Gmail }));

	public Task<ServiceResponse<EditApplicationDTO>> EditApplicationAsync(ApplicationsDTO editApplicationDTO)
	{
		var editApplication = new EditApplicationDTO
		{
			AppId = editApplicationDTO.applicationId,
			AppName = editApplicationDTO.applicationName,
			Description = editApplicationDTO.Description,
			IsActive = editApplicationDTO.IsActive
		};

		return PatchForAsync<EditApplicationDTO>("auth/editapplication", new { editApplication });
	}

	public Task<ServiceResponse<EditSubMenuDTO>> EditSubMenuAsync(SubMenusDTO editSubMenuDTO)
	{
		var editSubMenu = new EditSubMenuDTO
		{
			SubMenuId = editSubMenuDTO.subMenuId,
			SubMenuName = editSubMenuDTO.subMenuName,
			Description = editSubMenuDTO.Description,
			IsActive = editSubMenuDTO.IsActive
		};

		return PatchForAsync<EditSubMenuDTO>("auth/editsubmenu", new { editSubMenu });
	}

	public Task<ServiceResponse<EditRoleDTO>> EditRoleAsync(RolesDTO editRoleDTO)
	{
		var editRole = new EditRoleDTO
		{
			RoleId = editRoleDTO.roleId,
			RoleName = editRoleDTO.roleName,
			Description = editRoleDTO.Description
		};

		return PatchForAsync<EditRoleDTO>("auth/editrole", new { editRole });
	}

	public Task<ServiceResponse<EditAppSubRoleDTO>> EditAppSubRoleAsync(AppSubRolesDTO editAppSubRoleDTO)
	{
		var editAppSubRole = new EditAppSubRoleDTO
		{
			AppSubRoleId = editAppSubRoleDTO.AppRoleId,
			UserId = editAppSubRoleDTO.UserId,
			AppId = editAppSubRoleDTO.AppId,
			SubMenuId = editAppSubRoleDTO.SubMenuId,
			RoleId = editAppSubRoleDTO.RoleId,
		};

		return PatchForAsync<EditAppSubRoleDTO>("auth/editappsubrole", new { editAppSubRole });
	}

	public Task<ServiceResponse<EditUserDTO>> EditUserAsync(UnApprovedUsersDTO editUserDTO)
	{
		var editUser = new EditUserDTO
		{
			Email = editUserDTO.email,
			IsApproved = editUserDTO.isApproved
		};

		return PatchForAsync<EditUserDTO>("auth/edituser", new { editUser });
	}

	private static string BuildPagedQuery(string route, int? pageNumber, int? pageSize, string? searchTerm)
	{
		var query = $"{route}?pageNumber={pageNumber}&pageSize={pageSize}";
		if (!string.IsNullOrEmpty(searchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(searchTerm)}";

		return query;
	}

	private async Task<ServiceResponse<PaginatedResult<TItem>>> GetPagedAsync<TEnvelope, TItem>(
		string query,
		Func<TEnvelope, PaginatedResult<TItem>?> project,
		CancellationToken ct)
		where TItem : class
	{
		try
		{
			var response = await _httpClient.GetAsync(query, ct);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<PaginatedResult<TItem>>.Failure(await response.ReadErrorDetailAsync(ct));
			}

			var envelope = await response.Content.ReadFromJsonAsync<TEnvelope>(cancellationToken: ct);
			var result = envelope is null ? null : project(envelope);

			if (result is null)
			{
				return ServiceResponse<PaginatedResult<TItem>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<PaginatedResult<TItem>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<PaginatedResult<TItem>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	private async Task<ServiceResponse<bool>> SendForBoolAsync(Func<Task<HttpResponseMessage>> send)
	{
		try
		{
			var response = await send();

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<bool>();
			return ServiceResponse<bool>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	private async Task<ServiceResponse<T>> PatchForAsync<T>(string route, object payload)
		where T : class
	{
		try
		{
			var response = await _httpClient.PatchAsJsonAsync(route, payload);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<T>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<T>();

			if (result is null)
			{
				return ServiceResponse<T>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<T>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<T>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
