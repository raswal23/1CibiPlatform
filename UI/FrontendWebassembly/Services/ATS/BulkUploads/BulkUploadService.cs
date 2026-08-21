namespace FrontendWebassembly.Services.ATS.BulkUploads;

public class BulkUploadService : IBulkUploadService
{
	private readonly HttpClient _httpClient;

	public BulkUploadService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<BulkUploadListDTO>>> GetBulkUploadsAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? status = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null)
	{
		var query = $"ats/getbulkuploads?pageSize={pageSize}";

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
				return ServiceResponse<KeysetPaginatedResult<BulkUploadListDTO>>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetBulkUploadsResponseDTO>();

			if (result?.BulkUploads is null)
			{
				return ServiceResponse<KeysetPaginatedResult<BulkUploadListDTO>>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<BulkUploadListDTO>>.Success(result.BulkUploads);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<BulkUploadListDTO>>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<BulkUploadStatusCountsDTO>> GetStatusCountsAsync(
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null)
	{
		var query = "ats/getbulkuploadstatuscounts";
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
				return ServiceResponse<BulkUploadStatusCountsDTO>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetBulkUploadStatusCountsResponseDTO>();

			if (result?.Counts is null)
			{
				return ServiceResponse<BulkUploadStatusCountsDTO>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<BulkUploadStatusCountsDTO>.Success(result.Counts);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<BulkUploadStatusCountsDTO>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<BulkUploadSubjectsResultDTO>> GetSubjectsAsync(
		Guid fileId,
		string? cursor = null,
		int? pageSize = 10,
		string? emailStatus = null,
		string? searchTerm = null)
	{
		var query = $"ats/getbulkuploadsubjects?fileId={fileId:D}&pageSize={pageSize}";

		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}

		if (!string.IsNullOrWhiteSpace(emailStatus))
		{
			query += $"&emailStatus={Uri.EscapeDataString(emailStatus)}";
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<BulkUploadSubjectsResultDTO>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetBulkUploadSubjectsResponseDTO>();

			if (result?.Result?.Subjects is null)
			{
				return ServiceResponse<BulkUploadSubjectsResultDTO>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<BulkUploadSubjectsResultDTO>.Success(result.Result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<BulkUploadSubjectsResultDTO>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<BulkUploadSubjectCountsDTO>> GetSubjectCountsAsync(
		Guid fileId,
		string? searchTerm = null)
	{
		var query = $"ats/getbulkuploadsubjectcounts?fileId={fileId:D}";

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
		}

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<BulkUploadSubjectCountsDTO>.Failure(
					await response.ReadErrorDetailAsync());
			}

			var result = await response.Content
				.ReadFromJsonAsync<GetBulkUploadSubjectCountsResponseDTO>();

			if (result?.Counts is null)
			{
				return ServiceResponse<BulkUploadSubjectCountsDTO>.Failure(
					"The server returned an empty response.");
			}

			return ServiceResponse<BulkUploadSubjectCountsDTO>.Success(result.Counts);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<BulkUploadSubjectCountsDTO>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
	}

	// Returns the raw response so the caller can read both the bytes and the
	// server-chosen Content-Disposition filename.
	public async Task<ServiceResponse<HttpResponseMessage>> ExportSubjectsAsync(
		Guid fileId,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/exportbulkuploadsubjects?fileId={fileId:D}";

		try
		{
			var response = await _httpClient.GetAsync(query, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
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
