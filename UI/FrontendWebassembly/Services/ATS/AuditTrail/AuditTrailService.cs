namespace FrontendWebassembly.Services.ATS.AuditTrail;

public class AuditTrailService : IAuditTrailService
{
	private readonly HttpClient _httpClient;

	public AuditTrailService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>> GetAuditTrailAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? outcome = null,
		string? action = null,
		string? area = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null)
	{
		var query = $"ats/getaudittrail?pageSize={pageSize}";

		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}

		if (!string.IsNullOrWhiteSpace(outcome))
		{
			query += $"&outcome={Uri.EscapeDataString(outcome)}";
		}

		if (!string.IsNullOrWhiteSpace(action))
		{
			query += $"&action={Uri.EscapeDataString(action)}";
		}

		if (!string.IsNullOrWhiteSpace(area))
		{
			query += $"&area={Uri.EscapeDataString(area)}";
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
				return ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetAuditTrailResponseDTO>();

			if (result?.AuditEntries is null)
			{
				return ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>.Success(result.AuditEntries);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<AuditOutcomeCountsDTO>> GetOutcomeCountsAsync(
		string? action = null,
		string? area = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null)
	{
		var query = "ats/getauditoutcomecounts";
		var separator = '?';

		if (!string.IsNullOrWhiteSpace(action))
		{
			query += $"{separator}action={Uri.EscapeDataString(action)}";
			separator = '&';
		}

		if (!string.IsNullOrWhiteSpace(area))
		{
			query += $"{separator}area={Uri.EscapeDataString(area)}";
			separator = '&';
		}

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
				return ServiceResponse<AuditOutcomeCountsDTO>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetAuditOutcomeCountsResponseDTO>();

			if (result?.Counts is null)
			{
				return ServiceResponse<AuditOutcomeCountsDTO>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<AuditOutcomeCountsDTO>.Success(result.Counts);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<AuditOutcomeCountsDTO>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	// Returns the raw response so the caller can read both the bytes and the server-chosen
	// Content-Disposition filename, matching BulkUploadService.ExportSubjectsAsync.
	public async Task<ServiceResponse<HttpResponseMessage>> ExportAuditTrailAsync(
		string? outcome = null,
		string? action = null,
		string? area = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null,
		CancellationToken cancellationToken = default)
	{
		var query = "ats/exportaudittrail";
		var separator = '?';

		void Append(string name, string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return;
			}

			query += $"{separator}{name}={Uri.EscapeDataString(value)}";
			separator = '&';
		}

		Append("outcome", outcome);
		Append("action", action);
		Append("area", area);
		Append("searchTerm", searchTerm);
		Append("startDate", startDate?.ToString("yyyy-MM-dd"));
		Append("endDate", endDate?.ToString("yyyy-MM-dd"));

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				// Carries the server's detail through, so a 403 explains that the trail is
				// admin-only rather than showing a generic failure.
				return ServiceResponse<HttpResponseMessage>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			return ServiceResponse<HttpResponseMessage>.Success(response);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<HttpResponseMessage>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}
}
