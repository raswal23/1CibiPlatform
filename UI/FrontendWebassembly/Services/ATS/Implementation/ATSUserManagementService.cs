namespace FrontendWebassembly.Services.ATS.Implementation;

public class ATSUserManagementService : IATSUserManagementService
{
	private readonly HttpClient _httpClient;

	public ATSUserManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<IReadOnlyList<ATSUserLookupDTO>> GetAuthUsersAsync(
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync("ats/getauthusers", cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return (await response.Content.ReadFromJsonAsync<List<ATSUserLookupDTO>>(
			cancellationToken: cancellationToken))!;
	}

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync("ats/getuserclientassignments", cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return (await response.Content.ReadFromJsonAsync<List<UserClientDetailsDTO>>(
			cancellationToken: cancellationToken))!;
	}

	public async Task<UserClientDetailsDTO> AssignUserClientAsync(
		AssignATSUserClientDTO assignmentDTO,
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.PostAsJsonAsync(
			"ats/assignuserclient",
			new { assignment = assignmentDTO },
			cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return (await response.Content.ReadFromJsonAsync<UserClientDetailsDTO>(
			cancellationToken: cancellationToken))!;
	}

	public async Task<GetUsersResponseDTO> GetUsersAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/getusers?pageIndex={pageIndex}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(searchTerm))
			query += $"&search={Uri.EscapeDataString(searchTerm)}";

		var response = await _httpClient.GetAsync(query, cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return (await response.Content.ReadFromJsonAsync<GetUsersResponseDTO>(cancellationToken: cancellationToken))!;
	}

	public async Task<IReadOnlyList<int>> GetMyModuleIdsAsync(
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync("ats/getmymodules", cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return (await response.Content.ReadFromJsonAsync<List<int>>(
			cancellationToken: cancellationToken))!;
	}

	public async Task<int?> GetMyRoleIdAsync(
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync("ats/getmyroleid", cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		var result = await response.Content.ReadFromJsonAsync<GetMyRoleIdResponseDTO>(
			cancellationToken: cancellationToken);
		return result?.RoleId;
	}

	public async Task<bool> AddUserAsync(AddATSUserDTO userDTO, CancellationToken cancellationToken = default)
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

		var response = await _httpClient.PostAsJsonAsync("ats/adduser", new { users }, cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
	}

	public async Task<IReadOnlyList<UserDetailsDTO>> EditUserAsync(EditATSUserDTO userDTO, CancellationToken cancellationToken = default)
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

		var response = await _httpClient.PatchAsJsonAsync("ats/edituser", new { editUsers }, cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);

		return (await response.Content.ReadFromJsonAsync<List<UserDetailsDTO>>(cancellationToken: cancellationToken))!;
	}

	private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
			return;

		var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
		throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
	}
}
