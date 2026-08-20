namespace FrontendWebassembly.Services.ATS.Implementation;

public class ModuleManagementService : IModuleManagementService
{
	private readonly HttpClient _httpClient;

	public ModuleManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<ModuleDetailsDTO>>> GetModulesAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/getmodules?pageSize={pageSize}";
		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<ModuleDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetModulesResponseDTO>(cancellationToken: cancellationToken);

			if (result?.Modules is null)
			{
				return ServiceResponse<KeysetPaginatedResult<ModuleDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<ModuleDetailsDTO>>.Success(result.Modules);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<ModuleDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<ModuleDetailsDTO>>> GetAllModulesAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		string? cursor = null;
		var modules = new List<ModuleDetailsDTO>();

		while (true)
		{
			var pageResponse = await GetModulesAsync(cursor, pageSize, cancellationToken: cancellationToken);

			if (!pageResponse.IsSuccess || pageResponse.Data is null)
			{
				return ServiceResponse<IReadOnlyList<ModuleDetailsDTO>>.Failure(pageResponse.ErrorDetail);
			}

			var page = pageResponse.Data;
			modules.AddRange(page.Items);

			if (page.NextCursor is null || page.Items.Count == 0)
				return ServiceResponse<IReadOnlyList<ModuleDetailsDTO>>.Success(modules);

			cursor = page.NextCursor;
		}
	}

	public async Task<ServiceResponse<bool>> AddModuleAsync(AddATSModuleDTO moduleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { module = moduleDTO };

		try
		{
			var response = await _httpClient.PostAsJsonAsync("ats/addmodule", request, cancellationToken);

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

	public async Task<ServiceResponse<ModuleDetailsDTO>> EditModuleAsync(EditATSModuleDTO moduleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { editModule = moduleDTO };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/editmodule", request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<ModuleDetailsDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<ModuleDetailsDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<ModuleDetailsDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<ModuleDetailsDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<ModuleDetailsDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
