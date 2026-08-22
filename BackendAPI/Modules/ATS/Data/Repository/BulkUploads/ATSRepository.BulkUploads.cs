namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Same fairness rule for CSV parsing.
	private const int PerClientFileSliceSize = 3;

	public async Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails)
	{
		await _dbcontext.BulkUploadFileDetails.AddAsync(bulkUploadFileDetails);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync()
	{
		// Same claim pattern as the email queue: SKIP LOCKED lets a second worker step
		// over files another worker is claiming, and the Processing write keeps the
		// claim after this transaction ends so the same CSV is never parsed twice.
		return await _dbcontext.BulkUploadFileDetails
			.FromSqlRaw(
				"""
				WITH ranked AS (
					SELECT "FileID",
						   ROW_NUMBER() OVER (
							   PARTITION BY "ClientId"
							   ORDER BY "FileID") AS rn
					FROM ats."BulkUploadFileDetails"
					WHERE "Status" = {2}
				)
				UPDATE ats."BulkUploadFileDetails" t
				SET "Status" = {0},
					"ClaimedAt" = {1}
				WHERE t."FileID" IN (
					SELECT f."FileID"
					FROM ats."BulkUploadFileDetails" f
					WHERE f."FileID" IN (
						SELECT "FileID" FROM ranked WHERE rn <= {3})
					ORDER BY f."FileID"
					LIMIT {4}
					FOR UPDATE SKIP LOCKED
				)
				RETURNING t.*;
				""",
				BulkFileStatus.Processing,
				DateTime.UtcNow,
				BulkFileStatus.Pending,
				PerClientFileSliceSize,
				10)
			.AsNoTracking()
			.ToListAsync();
	}

	public async Task<int> ReleaseBulkFileClaimsAsync(List<BulkUploadFileDetails> bulkUploadFileDetails)
	{
		// A file that failed to process goes straight back to Pending so the next tick
		// retries it, rather than waiting for the stale-claim sweeper.
		var fileIds = bulkUploadFileDetails.Select(x => x.FileID).ToList();

		return await _dbcontext.BulkUploadFileDetails
			.Where(x => fileIds.Contains(x.FileID))
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.Status, x => BulkFileStatus.Pending)
				.SetProperty(x => x.ClaimedAt, x => null));
	}

	public async Task<int> ReleaseStaleBulkFileClaimsAsync(TimeSpan staleAfter)
	{
		// A crash mid-parse leaves files stuck in Processing with no live worker.
		var cutoff = DateTime.UtcNow.Subtract(staleAfter);

		return await _dbcontext.BulkUploadFileDetails
			.Where(x => x.Status == BulkFileStatus.Processing
					 && x.ClaimedAt != null
					 && x.ClaimedAt < cutoff)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.Status, x => BulkFileStatus.Pending)
				.SetProperty(x => x.ClaimedAt, x => null));
	}

	public async Task<bool> UpdateBulkFileDetailsStatusAsync(List<Guid> bulkUploadFileDetailIds, string status)
	{
		await _dbcontext.BulkUploadFileDetails
				.Where(x => bulkUploadFileDetailIds.Contains(x.FileID))
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.Status, x => status)
				.SetProperty(x => x.ClaimedAt, x => null));

		return true;
	}
}
