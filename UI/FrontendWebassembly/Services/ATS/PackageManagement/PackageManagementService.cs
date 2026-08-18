namespace FrontendWebassembly.Services.ATS.PackageManagement;

public class PackageManagementService : IPackageManagementService
{
	private readonly HttpClient _httpClient;

	public PackageManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<PaginatedResult<PackageDetailsDTO>>> GetPackagesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default, int? clientId = null)
	{
		var query = $"ats/getpackages?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}
		if (clientId is > 0)
		{
			query += $"&clientId={clientId.Value}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<PaginatedResult<PackageDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetPackagesResponseDTO>(cancellationToken: cancellationToken);

			if (result?.Packages is null)
			{
				return ServiceResponse<PaginatedResult<PackageDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<PaginatedResult<PackageDetailsDTO>>.Success(result.Packages);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<PaginatedResult<PackageDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<PackageDetailsDTO>>> GetAllPackagesAsync(CancellationToken cancellationToken = default, int? clientId = null)
	{
		const int pageSize = 100;
		var pageNumber = 1;
		var packages = new List<PackageDetailsDTO>();

		while (true)
		{
			var pageResponse = await GetPackagesAsync(pageNumber, pageSize, cancellationToken: cancellationToken, clientId: clientId);

			if (!pageResponse.IsSuccess || pageResponse.Data is null)
			{
				return ServiceResponse<IReadOnlyList<PackageDetailsDTO>>.Failure(pageResponse.ErrorDetail);
			}

			var page = pageResponse.Data;
			var pageItems = page.Data.ToArray();
			packages.AddRange(pageItems);

			if (packages.Count >= page.Count || pageItems.Length == 0)
				return ServiceResponse<IReadOnlyList<PackageDetailsDTO>>.Success(packages);

			pageNumber++;
		}
	}

	public async Task<ServiceResponse<bool>> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken = default)
	{
		var request = new { package = packageDTO };

		try
		{
			var response = await _httpClient.PostAsJsonAsync("ats/addpackage", request, cancellationToken);

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

	public async Task<ServiceResponse<PackageDetailsDTO>> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken = default)
	{
		var request = new { editPackage = packageDTO };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/editpackage", request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<PackageDetailsDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<PackageDetailsDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<PackageDetailsDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<PackageDetailsDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<PackageDetailsDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
