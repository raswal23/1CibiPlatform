namespace FrontendWebassembly.Services.ATS.BulkUploads;

public interface IBulkUploadService
{
	Task<ServiceResponse<KeysetPaginatedResult<BulkUploadListDTO>>> GetBulkUploadsAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? status = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null);

	Task<ServiceResponse<BulkUploadStatusCountsDTO>> GetStatusCountsAsync(
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null);

	// The drill-down. All three return a 404 detail when the file is unknown or outside
	// the caller's scope; the dialog surfaces that rather than showing an empty table.
	Task<ServiceResponse<BulkUploadSubjectsResultDTO>> GetSubjectsAsync(
		Guid fileId,
		string? cursor = null,
		int? pageSize = 10,
		string? emailStatus = null,
		string? searchTerm = null);

	Task<ServiceResponse<BulkUploadSubjectCountsDTO>> GetSubjectCountsAsync(
		Guid fileId,
		string? searchTerm = null);

	Task<ServiceResponse<HttpResponseMessage>> ExportSubjectsAsync(
		Guid fileId,
		CancellationToken cancellationToken = default);
}
