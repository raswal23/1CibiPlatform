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

	public async Task<PaginatedResult<UsersDTO>> GetUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default)
	{
		var query = $"auth/getusers?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<UsersResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<UsersDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<UsersDTO>()
			);
		}

		return response.users!;
	}

	public async Task<PaginatedResult<UnApprovedUsersDTO>> GetUnApprovedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default)
	{
		var query = $"auth/getunapprovedusers?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<UnApprovedUsersResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<UnApprovedUsersDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<UnApprovedUsersDTO>()
			);
		}

		return response.users!;
	}

	public async Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(int? PageNumber = 1, int? PageSize = int.MaxValue, string? SearchTerm = null, CancellationToken ct = default)
	{
		var query = $"auth/getapplications?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<ApplicationsResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<ApplicationsDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<ApplicationsDTO>()
			);
		}

		return response.applications!;
	}

	public async Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(
		int? PageNumber = 1,
		int? PageSize = 10,
		string?
		SearchTerm = null,
		CancellationToken ct = default)
	{
		var query = $"auth/getsubmenus?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<SubMenusResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<SubMenusDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<SubMenusDTO>()
			);
		}

		return response.submenus!;
	}

	public async Task<PaginatedResult<LockedUsersDTO>> GetLockedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default)
	{
		var query = $"auth/getlockedusers?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<LockedUsersResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<LockedUsersDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<LockedUsersDTO>()
			);
		}

		return response.lockedusers!;
	}

	public async Task<PaginatedResult<RolesDTO>> GetRolesAsync(
		int? PageNumber = 1,
		int? PageSize = 10,
		string? SearchTerm = null,
		CancellationToken ct = default)
	{
		var query = $"auth/getroles?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<RolesResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<RolesDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<RolesDTO>()
			);
		}

		return response.roles!;
	}

	public async Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(
		int? PageNumber = 1,
		int? PageSize = 10,
		string? SearchTerm = null,
		CancellationToken ct = default)
	{
		var query = $"auth/getappsubroles?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&SearchTerm={Uri.EscapeDataString(SearchTerm)}";

		var response = await _httpClient.GetFromJsonAsync<AppSubRolesResponseDTO>(query, ct);

		if (response == null)
		{
			return new PaginatedResult<AppSubRolesDTO>(
				pageIndex: PageNumber ?? 1,
				pageSize: PageSize ?? 10,
				count: 0,
				data: Enumerable.Empty<AppSubRolesDTO>()
			);
		}

		return response.appsubroles!;
	}

	public async Task<bool> DeleteApplicationAsync(int AppId)
	{
		var response = await _httpClient.DeleteAsync($"auth/deleteapplication/{AppId}");
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent!;
	}

	public async Task<bool> DeleteSubMenuAsync(int SubMenuId)
	{
		var response = await _httpClient.DeleteAsync($"auth/deletesubmenu/{SubMenuId}");
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent!;
	}

	public async Task<bool> DeleteRoleAsync(int RoleId)
	{
		var response = await _httpClient.DeleteAsync($"auth/deleterole/{RoleId}");
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent!;
	}

	public async Task<bool> DeleteUserAppSubRoleAsync(int AppSubRoleId)
	{
		var response = await _httpClient.DeleteAsync($"auth/deleteappsubrole/{AppSubRoleId}");
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent!;
	}

	public async Task<bool> DeleteLockedUserAsync(Guid lockedUserId)
	{
		var response = await _httpClient.DeleteAsync($"auth/deletelockeduser/{lockedUserId}");
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent!;
	}

	public async Task<bool> AddApplicationAsync(AddApplicationDTO application)
	{
		var payload = new
		{
			application
		};

		var response = await _httpClient.PostAsJsonAsync($"auth/addapplication", payload);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent;
	}

	public async Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu)
	{
		var payload = new
		{
			subMenu
		};

		var response = await _httpClient.PostAsJsonAsync($"auth/addsubmenu", payload);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent;
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO role)
	{
		var payload = new
		{
			role
		};

		var response = await _httpClient.PostAsJsonAsync($"auth/addrole", payload);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent;
	}

	public async Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
	{
		var payload = new
		{
			appSubRole
		};

		var response = await _httpClient.PostAsJsonAsync($"auth/addappsubrole", payload);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent;
	}

	public async Task<bool> SendNotificationAsync(AssignmentNotificationDTO accountNotificationDTO)
	{
		var payload = new { accountNotificationDTO };
		var response = await _httpClient.PostAsJsonAsync("account/notification", payload);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent;
	}

	public async Task<bool> SendApprovalNotificationAsync(string Gmail)
	{
		var payload = new { Gmail };
		var response = await _httpClient.PostAsJsonAsync("account/approvalnotification", payload);
		if (!response.IsSuccessStatusCode)
		{
			return false!;
		}
		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent;
	}

	public async Task<EditApplicationDTO> EditApplicationAsync(ApplicationsDTO editApplicationDTO)
	{
		var editApplication = new EditApplicationDTO
		{
			AppId = editApplicationDTO.applicationId,
			AppName = editApplicationDTO.applicationName,
			Description = editApplicationDTO.Description,
			IsActive = editApplicationDTO.IsActive
		};

		var payload = new
		{
			editApplication
		};

		var response = await _httpClient.PatchAsJsonAsync($"auth/editapplication", payload);
		if (!response.IsSuccessStatusCode)
		{
			return null!;
		}
		var successContent = await response.Content.ReadFromJsonAsync<EditApplicationDTO>();
		if (successContent != null)
		{
			return successContent;
		}
		return null!;
	}

	public async Task<EditSubMenuDTO> EditSubMenuAsync(SubMenusDTO editSubMenuDTO)
	{
		var editSubMenu = new EditSubMenuDTO
		{
			SubMenuId = editSubMenuDTO.subMenuId,
			SubMenuName = editSubMenuDTO.subMenuName,
			Description = editSubMenuDTO.Description,
			IsActive = editSubMenuDTO.IsActive
		};

		var payload = new
		{
			editSubMenu
		};

		var response = await _httpClient.PatchAsJsonAsync($"auth/editsubmenu", payload);
		if (!response.IsSuccessStatusCode)
		{
			return null!;
		}
		var successContent = await response.Content.ReadFromJsonAsync<EditSubMenuDTO>();
		if (successContent != null)
		{
			return successContent;
		}
		return null!;
	}

	public async Task<EditRoleDTO> EditRoleAsync(RolesDTO editRoleDTO)
	{
		var editRole = new EditRoleDTO
		{
			RoleId = editRoleDTO.roleId,
			RoleName = editRoleDTO.roleName,
			Description = editRoleDTO.Description
		};

		var payload = new
		{
			editRole
		};

		var response = await _httpClient.PatchAsJsonAsync($"auth/editrole", payload);
		if (!response.IsSuccessStatusCode)
		{
			return null!;
		}
		var successContent = await response.Content.ReadFromJsonAsync<EditRoleDTO>();
		if (successContent != null)
		{
			return successContent;
		}
		return null!;
	}

	public async Task<EditAppSubRoleDTO> EditAppSubRoleAsync(AppSubRolesDTO editAppSubRoleDTO)
	{
		var editAppSubRole = new EditAppSubRoleDTO
		{
			AppSubRoleId = editAppSubRoleDTO.AppRoleId,
			UserId = editAppSubRoleDTO.UserId,
			AppId = editAppSubRoleDTO.AppId,
			SubMenuId = editAppSubRoleDTO.SubMenuId,
			RoleId = editAppSubRoleDTO.RoleId,
		};

		var payload = new
		{
			editAppSubRole
		};

		var response = await _httpClient.PatchAsJsonAsync($"auth/editappsubrole", payload);
        if (!response.IsSuccessStatusCode)
        {
            return null!;
        }
        var successContent = await response.Content.ReadFromJsonAsync<EditAppSubRoleDTO>();
        if (successContent != null)
        {
            return successContent;
        }
        return null!;
    }

	public async Task<EditUserDTO> EditUserAsync(UnApprovedUsersDTO editUserDTO)
	{
		var editUser = new EditUserDTO
		{
			Email = editUserDTO.email,
			IsApproved = editUserDTO.isApproved
		};

		var payload = new
		{
			editUser
		};

		var response = await _httpClient.PatchAsJsonAsync($"auth/edituser", payload);
		if (!response.IsSuccessStatusCode)
		{
			return null!;
		}
		var successContent = await response.Content.ReadFromJsonAsync<EditUserDTO>();

		if (successContent != null)
		{
			return successContent;
		}
		return null!;
	}
}
