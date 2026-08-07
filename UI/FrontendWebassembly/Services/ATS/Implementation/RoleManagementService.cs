namespace FrontendWebassembly.Services.ATS.Implementation;

public class RoleManagementService : IRoleManagementService
{
	private readonly HttpClient _httpClient;

	public RoleManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(
		int? pageNumber = 1,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/getroles?pageNumber={pageNumber}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
		}

		var response = await _httpClient.GetAsync(query, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetRolesResponseDTO>(cancellationToken: cancellationToken);
		return result!.Roles!;
	}

	public async Task<bool> AddRoleAsync(AddATSRoleDTO roleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { role = roleDTO };

		var response = await _httpClient.PostAsJsonAsync("ats/addrole", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
	}

	public async Task<RoleDetailsDTO> EditRoleAsync(EditATSRoleDTO roleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { editRole = roleDTO };

		var response = await _httpClient.PatchAsJsonAsync("ats/editrole", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<RoleDetailsDTO>(cancellationToken: cancellationToken);
		return result!;
	}
}
