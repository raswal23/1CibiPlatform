namespace FrontendWebassembly.Services.ATS.Implementation;

public class PackageManagementService : IPackageManagementService
{
	private readonly HttpClient _httpClient;

	public PackageManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	{
		var query = $"ats/getpackages?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}

		var response = await _httpClient.GetAsync(query);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetPackagesResponseDTO>();
		return result!.Packages!;
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO)
	{
		var request = new { package = packageDTO };

		var response = await _httpClient.PostAsJsonAsync("ats/addpackage", request);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<bool>();
		return result;
	}

	public async Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO)
	{
		var request = new { editPackage = packageDTO };

		var response = await _httpClient.PatchAsJsonAsync("ats/editpackage", request);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<PackageDetailsDTO>();
		return result!;
	}
}
