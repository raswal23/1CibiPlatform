namespace FrontendWebassembly.Services.ATS.Implementation;

public class ReportService : IReportService
{
	private readonly HttpClient _httpClient;

	public ReportService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

    public async Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO)
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

    public async Task<PaginatedResult<ReportListDTO>> GetReportsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, string? SortColumn = null, bool SortDescending = false, DateTime? StartDate = null, DateTime? EndDate = null)
	{
		var query = $"ats/getreports?pageNumber={PageNumber}&pageSize={PageSize}";
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}
		if (!string.IsNullOrWhiteSpace(SortColumn))
		{
			query += $"&sortColumn={Uri.EscapeDataString(SortColumn)}&sortDescending={SortDescending}";
		}
		if (StartDate.HasValue)
		{
			query += $"&startDate={Uri.EscapeDataString(StartDate.Value.ToString("yyyy-MM-dd"))}";
		}
		if (EndDate.HasValue)
		{
			query += $"&endDate={Uri.EscapeDataString(EndDate.Value.ToString("yyyy-MM-dd"))}";
		}

		var response = await _httpClient.GetAsync(query);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetReportsResponseDTO>();
		return result!.Reports!;
	}

	public async Task<ATSResultDetailsDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId)
	{
		var response = await _httpClient.GetAsync($"ats/getreportresult?emailInvitationRequestId={emailInvitationRequestId}");

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		var result = await response.Content.ReadFromJsonAsync<GetReportResultResponseDTO>();
		return result!.ReportResult!;
	}

	public async Task<HttpResponseMessage> DownloadDocumentsAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken = default)
	{
		var request = new { downloadInvididualRequest };

		var response = await _httpClient.PostAsJsonAsync(
									"ats/downloadindividualreport",
									request,
									cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		return response;
	}

	public async Task<HttpResponseMessage> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken = default)
	{
		var request = new { downloadMultipleOrderRecordsRequest };

		var response = await _httpClient.PostAsJsonAsync(
									"ats/downloadmultipleorderrecords",
									request,
									cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			throw new Exception($"Error: {errorContent?.Title}\nTraceId: {errorContent?.TraceId}");
		}

		return response;
	}
}
