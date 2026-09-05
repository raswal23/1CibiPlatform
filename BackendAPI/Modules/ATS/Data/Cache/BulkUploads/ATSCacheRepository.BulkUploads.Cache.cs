namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public async Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails)
	{
		return await _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails);
	}

	public Task<bool> BulkUploadFileNameExistsAsync(string fileName, int? clientId, Guid? uploadedByUserId, CancellationToken cancellationToken) =>
		_atsRepository.BulkUploadFileNameExistsAsync(fileName, clientId, uploadedByUserId, cancellationToken);

	public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync()
	{
		return await _atsRepository.GetBulkUploadFileDetailsAsync();
	}

	public async Task<int> ReleaseBulkFileClaimsAsync(List<BulkUploadFileDetails> bulkUploadFileDetails)
	{
		return await _atsRepository.ReleaseBulkFileClaimsAsync(bulkUploadFileDetails);
	}

	public async Task<int> ReleaseStaleBulkFileClaimsAsync(TimeSpan staleAfter)
	{
		return await _atsRepository.ReleaseStaleBulkFileClaimsAsync(staleAfter);
	}

	public async Task<bool> UpdateBulkFileDetailsStatusAsync(List<Guid> bulkUploadFileDetailIds, string orderStatus)
	{
		return await _atsRepository.UpdateBulkFileDetailsStatusAsync(bulkUploadFileDetailIds, orderStatus);
	}

	public async Task<bool> RecordBulkFileRowOutcomeAsync(
		Guid fileId,
		int acceptedRowCount,
		IReadOnlyCollection<BulkUploadRejectedRowDTO> rejectedRows,
		CancellationToken cancellationToken)
	{
		return await _atsRepository.RecordBulkFileRowOutcomeAsync(
			fileId,
			acceptedRowCount,
			rejectedRows,
			cancellationToken);
	}
}
