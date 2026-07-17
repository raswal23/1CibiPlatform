namespace FrontendWebassembly.Services.ATS.Implementation;

public class ReportService : FrontendWebassembly.Services.ATS.Interface.IReportService
{
	private readonly HttpClient _httpClient;

	public ReportService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

    public async Task<bool> UploadReportAsync(FrontendWebassembly.DTO.ATS.ReportDetailsDTO reportDetailsDTO)
	{
		using var content = new MultipartFormDataContent();

		void AddString(string? value, string name)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				content.Add(new StringContent(value), name);
			}
		}

		void AddFile(IBrowserFile? file, string name)
		{
			if (file != null)
			{
				var fileStream = file.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024);
				var fileContent = new StreamContent(fileStream);
				fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
				content.Add(fileContent, name, file.Name);
			}
		}

		AddString(reportDetailsDTO.EmailInvitationRequestId.ToString(), "ReportDetailsDTO.EmailInvitationRequestId");
		AddString(reportDetailsDTO.HitStatus, "ReportDetailsDTO.HitStatus");
		AddString(reportDetailsDTO.ReportStatus, "ReportDetailsDTO.ReportStatus");
		AddFile(reportDetailsDTO.ReportFile, "ReportDetailsDTO.ReportFile");

		var response = await _httpClient.PostAsync("ats/uploadreport", content);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<bool>();
		return result;
	}

	public async Task<PaginatedResult<FrontendWebassembly.DTO.ATS.ReportListDTO>> GetReportsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	{
		var query = $"ats/getreports?pageNumber={PageNumber}&pageSize={PageSize}";
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

		var result = await response.Content.ReadFromJsonAsync<FrontendWebassembly.DTO.ATS.GetReportsResponseDTO>();
		return result!.Reports!;
	}
}
