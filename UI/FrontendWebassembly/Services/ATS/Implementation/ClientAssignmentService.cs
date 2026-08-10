namespace FrontendWebassembly.Services.ATS.Implementation;

public sealed class ClientAssignmentService : IClientAssignmentService
{
	private readonly HttpClient _httpClient;

	public ClientAssignmentService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<GetClientAssignmentsResponseDTO> GetAssignmentsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync(
			BuildPagedUri("ats/getclientassignments", pageIndex, pageSize, searchTerm),
			cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);
		return (await response.Content.ReadFromJsonAsync<GetClientAssignmentsResponseDTO>(
			cancellationToken: cancellationToken))!;
	}

	public async Task<GetClientLookupResponseDTO> GetAssignableClientsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync(
			BuildPagedUri("ats/getassignableclients", pageIndex, pageSize, searchTerm),
			cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);
		return (await response.Content.ReadFromJsonAsync<GetClientLookupResponseDTO>(
			cancellationToken: cancellationToken))!;
	}

	public async Task<ClientAssignmentDetailsDTO> AssignClientAsync(
		AssignATSUserClientDTO assignment,
		CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.PutAsJsonAsync(
			"ats/assignclient",
			new { assignment },
			cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);
		return (await response.Content.ReadFromJsonAsync<ClientAssignmentDetailsDTO>(
			cancellationToken: cancellationToken))!;
	}

	private static string BuildPagedUri(
		string route,
		int pageIndex,
		int pageSize,
		string? searchTerm)
	{
		var uri = $"{route}?pageIndex={pageIndex}&pageSize={pageSize}";
		return string.IsNullOrWhiteSpace(searchTerm)
			? uri
			: $"{uri}&search={Uri.EscapeDataString(searchTerm.Trim())}";
	}

	private static async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
			return;

		var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
			cancellationToken: cancellationToken);
		throw new Exception($"Error: {error?.Detail ?? error?.Title}\nTraceId: {error?.TraceId}");
	}
}
