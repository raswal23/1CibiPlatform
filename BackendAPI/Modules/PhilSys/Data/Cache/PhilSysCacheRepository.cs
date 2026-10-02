
namespace PhilSys.Data.Cache;

public class PhilSysCacheRepository : IPhilSysRepository
{
	private readonly IPhilSysRepository _philSysRepository;
	private readonly HybridCache _hybridCache;

	public PhilSysCacheRepository(IPhilSysRepository philSysRepository, HybridCache hybridCache)
	{
		_philSysRepository = philSysRepository;
		_hybridCache = hybridCache;
	}

	public async Task<bool> AddTransactionDataAsync(PhilSysTransaction PhilSysTransaction)
	{
		return await _philSysRepository.AddTransactionDataAsync(PhilSysTransaction);
	}

	public async Task<bool> AddTransactionResultDataAsync(PhilSysTransactionResult PhilSysTransactionResult)
	{
		return await _philSysRepository.AddTransactionResultDataAsync(PhilSysTransactionResult);
	}

	public async Task<bool> DeleteTransactionDataAsync(PhilSysTransaction HashToken)
	{
		var result = await _philSysRepository.DeleteTransactionDataAsync(HashToken);

		if (result)
		{
			await _hybridCache.RemoveAsync(LivenessStatusKey(HashToken.HashToken!));
		}

		return result;
	}

	public async Task<TransactionStatusResponse> GetLivenessSessionStatusAsync(string HashToken)
	{
		return await _hybridCache.GetOrCreateAsync<TransactionStatusResponse>(
			LivenessStatusKey(HashToken),
			async status => await _philSysRepository.GetLivenessSessionStatusAsync(HashToken));
	}

	public async Task<PhilSysTransaction> GetTransactionDataByHashTokenAsync(string HashToken)
	{
		return await _philSysRepository.GetTransactionDataByHashTokenAsync(HashToken);
	}

	public async Task<PhilSysTransaction> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSessionId)
	{
		return await _philSysRepository.UpdateFaceLivenessSessionAsync(HashToken, FaceLivenessSessionId);
	}

	public async Task<PhilSysTransaction> UpdateTransactionDataAsync(PhilSysTransaction Transaction)
	{
		var result = await _philSysRepository.UpdateTransactionDataAsync(Transaction);

		if (result != null)
		{
			await _hybridCache.RemoveAsync(LivenessStatusKey(Transaction.HashToken!));
		}

		return result;
	}

	public async Task<List<PhilSysTransaction>> GetExpiredUntransactedTransactionsAsync(DateTime cutoffUtc)
	{
		return await _philSysRepository.GetExpiredUntransactedTransactionsAsync(cutoffUtc);
	}

	private static string LivenessStatusKey(string HashToken) => $"PhilSys_LivenessSessionStatus_{HashToken}";
}
