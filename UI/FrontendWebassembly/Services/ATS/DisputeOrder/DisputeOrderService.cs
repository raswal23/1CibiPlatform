using FrontendWebassembly.Component.ATS;

namespace FrontendWebassembly.Services.ATS.DisputeOrder;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly HttpClient _httpClient;

	public DisputeOrderService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<DisputeOrderListDTO>>> GetDisputeOrdersAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null)
	{
		var query = $"ats/getdisputeorders?pageSize={pageSize}";
		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<DisputeOrderListDTO>>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetDisputeOrdersResponseDTO>();

			if (result?.Orders is null)
			{
				return ServiceResponse<KeysetPaginatedResult<DisputeOrderListDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<DisputeOrderListDTO>>.Success(result.Orders);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<DisputeOrderListDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest)
	{
		var request = new { disputeRequest };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/markasdisputed", request);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<bool>();
			return ServiceResponse<bool>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
