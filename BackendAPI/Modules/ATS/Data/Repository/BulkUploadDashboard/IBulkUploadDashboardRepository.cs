namespace ATS.Data.Repository.BulkUploadDashboard;

// Read side of ats.BulkUploadFileDetails. The write side and the background job's
// claim query stay on IBulkUploadRepository (the aggregate partial); this contract
// exists only for the dashboard.
public interface IBulkUploadDashboardRepository
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

	// Returns null when the file does not exist or falls outside the caller's scope.
	// The caller cannot distinguish the two cases, which is the point: a probe for a
	// file id must not reveal whether it exists.
	Task<BulkUploadHeaderDTO?> GetVisibleFileHeaderAsync(
		Guid fileId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken);

	// Scope is enforced once on the parent file by GetVisibleFileHeaderAsync, so the
	// three methods below filter on BulkFileID alone.
	Task<List<BulkUploadSubjectListDTO>> GetSubjectsPageAsync(
		Guid fileId,
		Guid? afterInvitationId,
		int take,
		string? emailStatus,
		string? searchTerm,
		CancellationToken cancellationToken);

	Task<long> CountSubjectsAsync(
		Guid fileId,
		string? emailStatus,
		string? searchTerm,
		CancellationToken cancellationToken);

	Task<BulkUploadSubjectCountsDTO> GetSubjectCountsAsync(
		Guid fileId,
		string? searchTerm,
		CancellationToken cancellationToken);

	// Unpaged, for the CSV export. Bounded by one file's row count, which is the size
	// of the uploaded CSV.
	Task<List<BulkUploadSubjectListDTO>> GetAllSubjectsForExportAsync(
		Guid fileId,
		CancellationToken cancellationToken);
}
