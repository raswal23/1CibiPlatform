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
}
