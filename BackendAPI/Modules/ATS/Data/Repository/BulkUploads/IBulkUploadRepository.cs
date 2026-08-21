namespace ATS.Data.Repository.BulkUploads;

// Read side of ats.BulkUploadFileDetails. The write side and the background job's
// claim query stay on IATSRepository; this contract exists only for the dashboard.
public interface IBulkUploadRepository
{
	Task<List<BulkUploadRowDTO>> GetBulkUploadsPageAsync(
		DateTime? afterDateCreated,
		Guid? afterFileId,
		int take,
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken);

	Task<long> CountBulkUploadsAsync(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken);

	Task<BulkUploadStatusCountsDTO> GetStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken);

	Task<List<BulkFileInvitationRollupDTO>> GetInvitationRollupAsync(
		IReadOnlyCollection<Guid> fileIds,
		CancellationToken cancellationToken);
}
