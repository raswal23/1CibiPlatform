namespace FrontendWebassembly.Services.ATS.Implementation;

public class ClientManagementService : IClientManagementService
{
	private readonly HttpClient _httpClient;

	public ClientManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<GetClientsResponseDTO> GetClientsAsync(int pageIndex = 1, int pageSize = 10, string? searchTerm = null, CancellationToken cancellationToken = default)
	{
		var query = $"ats/getclients?pageIndex={pageIndex}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&search={Uri.EscapeDataString(searchTerm)}";
		}

		var response = await _httpClient.GetAsync(query, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetClientsResponseDTO>(cancellationToken: cancellationToken);
		return result!;
	}

	public async Task<bool> AddClientAsync(AddClientDTO clientDTO, CancellationToken cancellationToken = default)
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

		var response = await _httpClient.PostAsJsonAsync("ats/addclient", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
		return result;
	}

	public async Task<IReadOnlyList<ClientDetailsDTO>> EditClientAsync(EditClientDTO clientDTO, CancellationToken cancellationToken = default)
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

		var response = await _httpClient.PatchAsJsonAsync("ats/editclient", request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<List<ClientDetailsDTO>>(cancellationToken: cancellationToken);
		return result!;
	}

	public async Task<IReadOnlyList<ClientDetailsDTO>> GetAllClientsAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		var pageIndex = 1;
		var clients = new List<ClientDetailsDTO>();

		while (true)
		{
			var page = await GetClientsAsync(pageIndex, pageSize, cancellationToken: cancellationToken);
			clients.AddRange(page.Items);

			if (clients.Select(client => client.ClientId).Distinct().LongCount() >= page.TotalRecords || page.Items.Count == 0)
				return clients;

			pageIndex++;
		}
	}
}
