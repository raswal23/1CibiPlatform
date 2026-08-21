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
}
