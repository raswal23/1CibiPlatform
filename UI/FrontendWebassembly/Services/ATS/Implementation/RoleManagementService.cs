namespace FrontendWebassembly.Services.ATS.Implementation;

public class RoleManagementService : IRoleManagementService
{
	private readonly HttpClient _httpClient;

	public RoleManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<PaginatedResult<RoleDetailsDTO>>> GetRolesAsync(
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

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<PaginatedResult<RoleDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetRolesResponseDTO>(cancellationToken: cancellationToken);

			if (result?.Roles is null)
			{
				return ServiceResponse<PaginatedResult<RoleDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<PaginatedResult<RoleDetailsDTO>>.Success(result.Roles);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<PaginatedResult<RoleDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<RoleDetailsDTO>>> GetAllRolesAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		var pageNumber = 1;
		var roles = new List<RoleDetailsDTO>();

		while (true)
		{
			var pageResponse = await GetRolesAsync(pageNumber, pageSize, cancellationToken: cancellationToken);

			if (!pageResponse.IsSuccess || pageResponse.Data is null)
			{
				return ServiceResponse<IReadOnlyList<RoleDetailsDTO>>.Failure(pageResponse.ErrorDetail);
			}

			var page = pageResponse.Data;
			var pageItems = page.Data.ToArray();
			roles.AddRange(pageItems);

			if (roles.Count >= page.Count || pageItems.Length == 0)
				return ServiceResponse<IReadOnlyList<RoleDetailsDTO>>.Success(roles);

			pageNumber++;
		}
	}

	public async Task<ServiceResponse<bool>> AddRoleAsync(AddATSRoleDTO roleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { role = roleDTO };

		try
		{
			var response = await _httpClient.PostAsJsonAsync("ats/addrole", request, cancellationToken);

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

	public async Task<ServiceResponse<RoleDetailsDTO>> EditRoleAsync(EditATSRoleDTO roleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { editRole = roleDTO };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/editrole", request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<RoleDetailsDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<RoleDetailsDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<RoleDetailsDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<RoleDetailsDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<RoleDetailsDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
