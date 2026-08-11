namespace FrontendWebassembly.Services.ATS.Implementation;

public class PackageManagementService : IPackageManagementService
{
	private readonly HttpClient _httpClient;

	public PackageManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default)
	{
		var query = $"ats/getpackages?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}

		var response = await _httpClient.GetAsync(query, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetPackagesResponseDTO>(cancellationToken: cancellationToken);
		return result!.Packages!;
	}

	public async Task<IReadOnlyList<PackageDetailsDTO>> GetAllPackagesAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		var pageNumber = 1;
		var packages = new List<PackageDetailsDTO>();

		while (true)
		{
			var page = await GetPackagesAsync(pageNumber, pageSize, cancellationToken: cancellationToken);
			var pageItems = page.Data.ToArray();
			packages.AddRange(pageItems);

			if (packages.Count >= page.Count || pageItems.Length == 0)
				return packages;

			pageNumber++;
		}
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken = default)
	{
		var request = new { package = packageDTO };

		var response = await _httpClient.PostAsJsonAsync("ats/addpackage", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
		return result;
	}

	public async Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken = default)
	{
		var request = new { editPackage = packageDTO };

		var response = await _httpClient.PatchAsJsonAsync("ats/editpackage", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<PackageDetailsDTO>(cancellationToken: cancellationToken);
		return result!;
	}
}
