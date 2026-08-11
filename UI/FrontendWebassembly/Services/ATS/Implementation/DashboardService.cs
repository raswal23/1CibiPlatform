namespace FrontendWebassembly.Services.ATS.Implementation;

public class DashboardService : IDashboardService
{
	private readonly HttpClient _httpClient;

	public DashboardService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ATSDashboardDTO> GetDashboardAsync(string? requester = null)
	{
		var query = "ats/getdashboard";
		if (!string.IsNullOrWhiteSpace(requester))
		{
			query += $"?requester={Uri.EscapeDataString(requester)}";
		}

		var response = await _httpClient.GetAsync(query);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetATSDashboardResponseDTO>();
		return result?.Dashboard ?? new ATSDashboardDTO();
	}
}
