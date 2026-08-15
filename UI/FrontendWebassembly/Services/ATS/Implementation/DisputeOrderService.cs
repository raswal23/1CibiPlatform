using FrontendWebassembly.Component.ATS;

namespace FrontendWebassembly.Services.ATS.Implementation;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly HttpClient _httpClient;

	public DisputeOrderService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<PaginatedResult<DisputeOrderListDTO>>> GetDisputeOrdersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	{
		var query = $"ats/getdisputeorders?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<PaginatedResult<DisputeOrderListDTO>>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetDisputeOrdersResponseDTO>();

			if (result?.Orders is null)
			{
				return ServiceResponse<PaginatedResult<DisputeOrderListDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<PaginatedResult<DisputeOrderListDTO>>.Success(result.Orders);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<PaginatedResult<DisputeOrderListDTO>>.Failure($"Unable to reach the server. {ex.Message}");
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
