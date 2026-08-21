namespace ATS.Services.BulkUploadMonitoring;

public interface IBulkUploadMonitoringService
{
	Task<KeysetPaginatedResult<BulkUploadListDTO>> GetBulkUploadsAsync(
		KeysetPaginationRequest paginationRequest,
		string? status,
		CancellationToken cancellationToken);

	Task<BulkUploadStatusCountsDTO> GetStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken);

	// The three drill-down reads below throw NotFoundException when the file is unknown
	// or outside the caller's scope, rather than returning an empty page - an empty list
	// would be indistinguishable from a file whose CSV has not been parsed yet.
	Task<BulkUploadSubjectsResultDTO> GetSubjectsAsync(
		Guid fileId,
		KeysetPaginationRequest paginationRequest,
		string? emailStatus,
		CancellationToken cancellationToken);

	Task<BulkUploadSubjectCountsDTO> GetSubjectCountsAsync(
		Guid fileId,
		string? searchTerm,
		CancellationToken cancellationToken);

	Task<BulkUploadSubjectExportDTO> ExportSubjectsAsync(
		Guid fileId,
		CancellationToken cancellationToken);
}
