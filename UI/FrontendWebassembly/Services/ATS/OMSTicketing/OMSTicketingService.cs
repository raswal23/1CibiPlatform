namespace FrontendWebassembly.Services.ATS.OMSTicketing;

public class OMSTicketingService : IOMSTicketingService
{
	private readonly HttpClient _httpClient;

	public OMSTicketingService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<TicketedOrderListDTO>>> GetTicketedOrdersAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? status = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null)
	{
		var query = $"ats/getticketedorders?pageSize={pageSize}";

		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}

		if (!string.IsNullOrWhiteSpace(status))
		{
			query += $"&status={Uri.EscapeDataString(status)}";
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
		}

		if (startDate.HasValue)
		{
			query += $"&startDate={Uri.EscapeDataString(startDate.Value.ToString("yyyy-MM-dd"))}";
		}

		if (endDate.HasValue)
		{
			query += $"&endDate={Uri.EscapeDataString(endDate.Value.ToString("yyyy-MM-dd"))}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<TicketedOrderListDTO>>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetTicketedOrdersResponseDTO>();

			if (result?.TicketedOrders is null)
			{
				return ServiceResponse<KeysetPaginatedResult<TicketedOrderListDTO>>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<TicketedOrderListDTO>>.Success(result.TicketedOrders);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<TicketedOrderListDTO>>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<TicketStatusCountsDTO>> GetStatusCountsAsync(
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null)
	{
		var query = "ats/getticketstatuscounts";
		var separator = '?';

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"{separator}searchTerm={Uri.EscapeDataString(searchTerm)}";
			separator = '&';
		}

		if (startDate.HasValue)
		{
			query += $"{separator}startDate={Uri.EscapeDataString(startDate.Value.ToString("yyyy-MM-dd"))}";
			separator = '&';
		}

		if (endDate.HasValue)
		{
			query += $"{separator}endDate={Uri.EscapeDataString(endDate.Value.ToString("yyyy-MM-dd"))}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<TicketStatusCountsDTO>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetTicketStatusCountsResponseDTO>();

			if (result?.Counts is null)
			{
				return ServiceResponse<TicketStatusCountsDTO>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<TicketStatusCountsDTO>.Success(result.Counts);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<TicketStatusCountsDTO>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> RetryTicketAsync(Guid emailInvitationId)
	{
		var request = new { emailInvitationId };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/retryticket", request);

			if (!response.IsSuccessStatusCode)
			{
				// Carries the server's detail through, so a 409 explains that the row
				// moved on rather than showing a generic failure.
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync());
			}

			var successContent = await response.Content.ReadFromJsonAsync<bool>();

			return ServiceResponse<bool>.Success(successContent);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
