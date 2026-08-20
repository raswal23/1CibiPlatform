namespace FrontendWebassembly.Services.ATS.Implementation;

public class ReportService : IReportService
{
	private readonly HttpClient _httpClient;

	public ReportService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

    public async Task<ServiceResponse<bool>> UploadReportAsync(ReportDetailsDTO reportDetailsDTO)
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

		try
		{
			var response = await _httpClient.PostAsync("ats/uploadreport", content);

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

    public async Task<ServiceResponse<KeysetPaginatedResult<ReportListDTO>>> GetReportsAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, DateTime? StartDate = null, DateTime? EndDate = null)
	{
		var query = $"ats/getreports?pageSize={pageSize}";
		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}
		if (!string.IsNullOrWhiteSpace(SearchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";
		}
		if (StartDate.HasValue)
		{
			query += $"&startDate={Uri.EscapeDataString(StartDate.Value.ToString("yyyy-MM-dd"))}";
		}
		if (EndDate.HasValue)
		{
			query += $"&endDate={Uri.EscapeDataString(EndDate.Value.ToString("yyyy-MM-dd"))}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<ReportListDTO>>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetReportsResponseDTO>();

			if (result?.Reports is null)
			{
				return ServiceResponse<KeysetPaginatedResult<ReportListDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<ReportListDTO>>.Success(result.Reports);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<ReportListDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<ATSResultDetailsDTO>> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId)
	{
		try
		{
			var response = await _httpClient.GetAsync($"ats/getreportresult?emailInvitationRequestId={emailInvitationRequestId}");

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<ATSResultDetailsDTO>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetReportResultResponseDTO>();

			if (result?.ReportResult is null)
			{
				return ServiceResponse<ATSResultDetailsDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<ATSResultDetailsDTO>.Success(result.ReportResult);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<ATSResultDetailsDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<HttpResponseMessage>> DownloadDocumentsAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken = default)
	{
		var request = new { downloadInvididualRequest };

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
										"ats/downloadindividualreport",
										request,
										cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<HttpResponseMessage>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			return ServiceResponse<HttpResponseMessage>.Success(response);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<HttpResponseMessage>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<HttpResponseMessage>> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken = default)
	{
		var request = new { downloadMultipleOrderRecordsRequest };

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
										"ats/downloadmultipleorderrecords",
										request,
										cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<HttpResponseMessage>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			return ServiceResponse<HttpResponseMessage>.Success(response);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<HttpResponseMessage>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<IReadOnlyList<OrderStatusHistoryDTO>>> GetOrderStatusHistoryAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync(
				$"ats/getorderstatushistory?emailInvitationRequestId={emailInvitationRequestId}",
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<IReadOnlyList<OrderStatusHistoryDTO>>.Failure(await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content
				.ReadFromJsonAsync<GetOrderStatusHistoryResponseDTO>(
					cancellationToken: cancellationToken);

			return ServiceResponse<IReadOnlyList<OrderStatusHistoryDTO>>.Success(result?.History ?? []);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<IReadOnlyList<OrderStatusHistoryDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
