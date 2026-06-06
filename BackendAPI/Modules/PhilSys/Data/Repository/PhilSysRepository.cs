namespace PhilSys.Data.Repository;

public class PhilSysRepository : IPhilSysRepository
{
	private readonly PhilSysDBContext _dbcontext;

	public PhilSysRepository(PhilSysDBContext dbcontext)
	{
		_dbcontext = dbcontext;
	}

	public async Task<bool> AddTransactionDataAsync(PhilSysTransaction PhilSysTransaction)
	{
		await _dbcontext.PhilSysTransactions.AddAsync(PhilSysTransaction);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<PhilSysTransaction> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSessionId)
	{
		var transaction = await _dbcontext.PhilSysTransactions.FirstOrDefaultAsync(x => x.HashToken == HashToken);

		transaction!.FaceLivenessSessionId = FaceLivenessSessionId;

		transaction.ImageinByte = Photo;

		transaction.UpdatedLivenessIdAt = DateTime.UtcNow;

		await _dbcontext.SaveChangesAsync();

		return transaction;
	}

	public async Task<PhilSysTransaction> GetTransactionDataByHashTokenAsync(string HashToken)
	{
		var transaction = await _dbcontext.PhilSysTransactions.FirstOrDefaultAsync(x => x.HashToken == HashToken);

		return transaction!;
	}
	
	public async Task<TransactionStatusResponse> GetLivenessSessionStatusAsync(string HashToken)
	{
		var transaction = await _dbcontext.PhilSysTransactions
		.AsNoTracking()
		.Where(t => t.HashToken == HashToken)
		.Select(t => new TransactionStatusResponse
		{
			Exists = true, 
			WebHookURl = t.WebHookUrl,
			IsTransacted = t.IsTransacted,
			isExpired = false,
			ExpiresAt = t.ExpiresAt
		})
		.FirstOrDefaultAsync();

		return transaction ?? new TransactionStatusResponse { Exists = false };
	}

	public async Task<bool> DeleteTransactionDataAsync(PhilSysTransaction transaction)
	{
		_dbcontext.PhilSysTransactions.Remove(transaction!);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<PhilSysTransaction> UpdateTransactionDataAsync(PhilSysTransaction transaction)
	{
		transaction.IsTransacted = true;
		transaction.TransactedAt = DateTime.UtcNow;

		_dbcontext.PhilSysTransactions.Update(transaction);

		await _dbcontext.SaveChangesAsync();

		return transaction;
	}

	public async Task<bool> AddTransactionResultDataAsync(PhilSysTransactionResult philSysTransactionResult)
	{
		await _dbcontext.PhilSysTransactionResults.AddAsync(philSysTransactionResult);
		return true;
	}
}
