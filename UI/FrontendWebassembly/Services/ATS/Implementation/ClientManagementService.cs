namespace FrontendWebassembly.Services.ATS.Implementation;

public class ClientManagementService : IClientManagementService
{
	private readonly HttpClient _httpClient;

	public ClientManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<GetClientsResponseDTO>> GetClientsAsync(int pageIndex = 1, int pageSize = 10, string? searchTerm = null, CancellationToken cancellationToken = default)
	{
		var query = $"ats/getclients?pageIndex={pageIndex}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&search={Uri.EscapeDataString(searchTerm)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<GetClientsResponseDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetClientsResponseDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<GetClientsResponseDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<GetClientsResponseDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<GetClientsResponseDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> AddClientAsync(AddClientDTO clientDTO, CancellationToken cancellationToken = default)
	{
		var clients = clientDTO.PackageIds
			.Distinct()
			.Select(packageId => new
			{
				clientDTO.ClientName,
				clientDTO.ClientDescription,
				clientDTO.IsActive,
				PackageId = packageId
			})
			.ToArray();
		var request = new { clients };

		try
		{
			var response = await _httpClient.PostAsJsonAsync("ats/addclient", request, cancellationToken);

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

	public async Task<ServiceResponse<IReadOnlyList<ClientDetailsDTO>>> EditClientAsync(EditClientDTO clientDTO, CancellationToken cancellationToken = default)
	{
		var editClients = clientDTO.PackageIds
			.Distinct()
			.Select(packageId => new
			{
				clientDTO.ClientId,
				clientDTO.ClientName,
				clientDTO.ClientDescription,
				clientDTO.IsActive,
				PackageId = packageId
			})
			.ToArray();
		var request = new { editClients };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/editclient", request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<IReadOnlyList<ClientDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<List<ClientDetailsDTO>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<IReadOnlyList<ClientDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<IReadOnlyList<ClientDetailsDTO>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<IReadOnlyList<ClientDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<ClientDetailsDTO>>> GetAllClientsAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		var pageIndex = 1;
		var clients = new List<ClientDetailsDTO>();

		while (true)
		{
			var pageResponse = await GetClientsAsync(pageIndex, pageSize, cancellationToken: cancellationToken);

			if (!pageResponse.IsSuccess || pageResponse.Data is null)
			{
				return ServiceResponse<IReadOnlyList<ClientDetailsDTO>>.Failure(pageResponse.ErrorDetail);
			}

			var page = pageResponse.Data;
			clients.AddRange(page.Items);

			if (clients.Select(client => client.ClientId).Distinct().LongCount() >= page.TotalRecords || page.Items.Count == 0)
				return ServiceResponse<IReadOnlyList<ClientDetailsDTO>>.Success(clients);

			pageIndex++;
		}
	}
}
