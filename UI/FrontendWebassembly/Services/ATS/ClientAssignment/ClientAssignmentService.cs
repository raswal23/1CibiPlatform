namespace FrontendWebassembly.Services.ATS.ClientAssignment;

public sealed class ClientAssignmentService : IClientAssignmentService
{
	private readonly HttpClient _httpClient;

	public ClientAssignmentService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>> GetAssignmentsAsync(
		string? cursor,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync(
				BuildPagedUri("/ats/getclientassignments", cursor, pageSize, searchTerm),
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<ClientLookupDTO>>> GetAssignableClientsAsync(
		string? cursor,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync(
				BuildPagedUri("ats/getassignableclients", cursor, pageSize, searchTerm),
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<ClientLookupDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<KeysetPaginatedResult<ClientLookupDTO>>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<KeysetPaginatedResult<ClientLookupDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<ClientLookupDTO>>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<ClientLookupDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<ClientAssignmentDetailsDTO>> AssignClientAsync(
		AssignATSUserClientDTO assignment,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.PutAsJsonAsync(
				"ats/assignclient",
				new { assignment },
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<ClientAssignmentDetailsDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<ClientAssignmentDetailsDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<ClientAssignmentDetailsDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<ClientAssignmentDetailsDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<ClientAssignmentDetailsDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	private static string BuildPagedUri(
		string route,
		string? cursor,
		int pageSize,
		string? searchTerm)
	{
		var uri = $"{route}?pageSize={pageSize}";
		if (!string.IsNullOrEmpty(cursor))
		{
			uri += $"&cursor={Uri.EscapeDataString(cursor)}";
		}

		return string.IsNullOrWhiteSpace(searchTerm)
			? uri
			: $"{uri}&search={Uri.EscapeDataString(searchTerm.Trim())}";
	}
}
