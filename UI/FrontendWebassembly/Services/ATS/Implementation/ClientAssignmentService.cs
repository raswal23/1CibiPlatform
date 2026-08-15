namespace FrontendWebassembly.Services.ATS.Implementation;

public sealed class ClientAssignmentService : IClientAssignmentService
{
	private readonly HttpClient _httpClient;

	public ClientAssignmentService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<GetClientAssignmentsResponseDTO>> GetAssignmentsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync(
				BuildPagedUri("/ats/getclientassignments", pageIndex, pageSize, searchTerm),
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<GetClientAssignmentsResponseDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetClientAssignmentsResponseDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<GetClientAssignmentsResponseDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<GetClientAssignmentsResponseDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<GetClientAssignmentsResponseDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<GetClientLookupResponseDTO>> GetAssignableClientsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync(
				BuildPagedUri("ats/getassignableclients", pageIndex, pageSize, searchTerm),
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<GetClientLookupResponseDTO>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content.ReadFromJsonAsync<GetClientLookupResponseDTO>(cancellationToken: cancellationToken);

			if (result is null)
			{
				return ServiceResponse<GetClientLookupResponseDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<GetClientLookupResponseDTO>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<GetClientLookupResponseDTO>.Failure($"Unable to reach the server. {ex.Message}");
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
		int pageIndex,
		int pageSize,
		string? searchTerm)
	{
		var uri = $"{route}?pageIndex={pageIndex}&pageSize={pageSize}";
		return string.IsNullOrWhiteSpace(searchTerm)
			? uri
			: $"{uri}&search={Uri.EscapeDataString(searchTerm.Trim())}";
	}
}
