namespace FrontendWebassembly.Services.ATS.Implementation;

public class DashboardService : IDashboardService
{
	private readonly HttpClient _httpClient;

	public DashboardService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<ATSDashboardDTO>> GetDashboardAsync(string? requester = null)
	{
		var query = "ats/getdashboard";
		if (!string.IsNullOrWhiteSpace(requester))
		{
			query += $"?requester={Uri.EscapeDataString(requester)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<ATSDashboardDTO>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetATSDashboardResponseDTO>();
			return ServiceResponse<ATSDashboardDTO>.Success(result?.Dashboard ?? new ATSDashboardDTO());
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<ATSDashboardDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
