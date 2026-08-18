namespace FrontendWebassembly.Services.ATS.ATSUserManagement;

public class ATSUserManagementService : IATSUserManagementService
{
	private readonly HttpClient _httpClient;

	public ATSUserManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<IReadOnlyList<ATSUserLookupDTO>>> GetAuthUsersAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/getauthusers", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<IReadOnlyList<ATSUserLookupDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<List<ATSUserLookupDTO>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<IReadOnlyList<ATSUserLookupDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<IReadOnlyList<ATSUserLookupDTO>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<IReadOnlyList<ATSUserLookupDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<UserClientDetailsDTO>>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/getuserclientassignments", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<IReadOnlyList<UserClientDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<List<UserClientDetailsDTO>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<IReadOnlyList<UserClientDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<IReadOnlyList<UserClientDetailsDTO>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<IReadOnlyList<UserClientDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<UserClientDetailsDTO>> AssignUserClientAsync(
		AssignATSUserClientDTO assignmentDTO,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"ats/assignuserclient",
				new { assignment = assignmentDTO },
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<UserClientDetailsDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<UserClientDetailsDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<UserClientDetailsDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<UserClientDetailsDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<UserClientDetailsDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<GetUsersResponseDTO>> GetUsersAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/getusers?pageIndex={pageIndex}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(searchTerm))
			query += $"&search={Uri.EscapeDataString(searchTerm)}";

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<GetUsersResponseDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetUsersResponseDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<GetUsersResponseDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<GetUsersResponseDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<GetUsersResponseDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<int>>> GetMyModuleIdsAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/getmymodules", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<IReadOnlyList<int>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<List<int>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<IReadOnlyList<int>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<IReadOnlyList<int>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<IReadOnlyList<int>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<int?>> GetMyRoleIdAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/getmyroleid", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<int?>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetMyRoleIdResponseDTO>(cancellationToken: cancellationToken);
			return ServiceResponse<int?>.Success(result?.RoleId);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<int?>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<GetMyAtsAccessResponseDTO>> GetMyAtsAccessAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/get-my-access", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<GetMyAtsAccessResponseDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetMyAtsAccessResponseDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<GetMyAtsAccessResponseDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<GetMyAtsAccessResponseDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<GetMyAtsAccessResponseDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> AddUserAsync(AddATSUserDTO userDTO, CancellationToken cancellationToken = default)
	{
		var users = userDTO.ModuleIds
			.Distinct()
			.Select(moduleId => new
			{
				userDTO.UserId,
				userDTO.UserName,
				userDTO.UserEmail,
				userDTO.IsActive,
				userDTO.ClientId,
				userDTO.Site,
				userDTO.RoleId,
				ModuleId = moduleId
			})
			.ToArray();

		try
		{
			var response = await _httpClient.PostAsJsonAsync("ats/adduser", new { users }, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
			return ServiceResponse<bool>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<UserDetailsDTO>>> EditUserAsync(EditATSUserDTO userDTO, CancellationToken cancellationToken = default)
	{
		var editUsers = userDTO.ModuleIds
			.Distinct()
			.Select(moduleId => new
			{
				userDTO.UserId,
				userDTO.UserName,
				userDTO.UserEmail,
				userDTO.IsActive,
				userDTO.ClientId,
				userDTO.Site,
				userDTO.RoleId,
				ModuleId = moduleId
			})
			.ToArray();

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/edituser", new { editUsers }, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<IReadOnlyList<UserDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<List<UserDetailsDTO>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<IReadOnlyList<UserDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<IReadOnlyList<UserDetailsDTO>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<IReadOnlyList<UserDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
