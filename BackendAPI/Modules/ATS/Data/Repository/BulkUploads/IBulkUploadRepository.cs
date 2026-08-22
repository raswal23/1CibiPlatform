namespace ATS.Data.Repository;

public interface IBulkUploadRepository
{
	Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails);
	Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync();
	Task<int> ReleaseBulkFileClaimsAsync(List<BulkUploadFileDetails> bulkUploadFileDetails);
	Task<int> ReleaseStaleBulkFileClaimsAsync(TimeSpan staleAfter);
	Task<bool> UpdateBulkFileDetailsStatusAsync(List<Guid> bulkUploadFileDetailIds, string orderStatus);
}
