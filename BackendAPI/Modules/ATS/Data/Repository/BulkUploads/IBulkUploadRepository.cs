namespace ATS.Data.Repository;

public interface IBulkUploadRepository
{
	Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails);
	Task<bool> BulkUploadFileNameExistsAsync(string fileName, int? clientId, Guid? uploadedByUserId, CancellationToken cancellationToken);
	Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync();
	Task<int> ReleaseBulkFileClaimsAsync(List<BulkUploadFileDetails> bulkUploadFileDetails);
	Task<int> ReleaseStaleBulkFileClaimsAsync(TimeSpan staleAfter);
	Task<bool> UpdateBulkFileDetailsStatusAsync(List<Guid> bulkUploadFileDetailIds, string orderStatus);

	/// <summary>
	/// Records how many rows of a parsed file became orders and which were refused.
	/// The file is parsed long after its upload response returned, so this is how the
	/// uploader finds out that rows were dropped.
	/// </summary>
	Task<bool> RecordBulkFileRowOutcomeAsync(
		Guid fileId,
		int acceptedRowCount,
		IReadOnlyCollection<BulkUploadRejectedRowDTO> rejectedRows,
		CancellationToken cancellationToken);
}
