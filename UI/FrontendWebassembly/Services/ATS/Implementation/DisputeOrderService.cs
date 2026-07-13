namespace FrontendWebassembly.Services.ATS.Implementation;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly HttpClient _httpClient;

	public DisputeOrderService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	{
		var query = $"ats/getdisputeorders?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}

		var response = await _httpClient.GetAsync(query);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetDisputeOrdersResponseDTO>();
		return result!.Orders!;
	}

	public async Task<bool> MarkAsDisputedAsync(Guid emailInvitationId)
	{
		var request = new { emailInvitationId };

		var response = await _httpClient.PatchAsJsonAsync("ats/markasdisputed", request);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<bool>();
		return result;
	}
}
