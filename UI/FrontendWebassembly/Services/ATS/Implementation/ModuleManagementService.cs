namespace FrontendWebassembly.Services.ATS.Implementation;

public class ModuleManagementService : IModuleManagementService
{
	private readonly HttpClient _httpClient;

	public ModuleManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(
		int? pageNumber = 1,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/getmodules?pageNumber={pageNumber}&pageSize={pageSize}";
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

		var result = await response.Content.ReadFromJsonAsync<GetModulesResponseDTO>(cancellationToken: cancellationToken);
		return result!.Modules!;
	}

	public async Task<IReadOnlyList<ModuleDetailsDTO>> GetAllModulesAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		var pageNumber = 1;
		var modules = new List<ModuleDetailsDTO>();

		while (true)
		{
			var page = await GetModulesAsync(pageNumber, pageSize, cancellationToken: cancellationToken);
			var pageItems = page.Data.ToArray();
			modules.AddRange(pageItems);

			if (modules.Count >= page.Count || pageItems.Length == 0)
				return modules;

			pageNumber++;
		}
	}

	public async Task<bool> AddModuleAsync(AddATSModuleDTO moduleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { module = moduleDTO };

		var response = await _httpClient.PostAsJsonAsync("ats/addmodule", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception(errorContent?.Detail ?? "Unable to add the module.");
		}

		return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
	}

	public async Task<ModuleDetailsDTO> EditModuleAsync(EditATSModuleDTO moduleDTO, CancellationToken cancellationToken = default)
	{
		var request = new { editModule = moduleDTO };

		var response = await _httpClient.PatchAsJsonAsync("ats/editmodule", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<ModuleDetailsDTO>(cancellationToken: cancellationToken);
		return result!;
	}
}
