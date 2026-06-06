
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
		return await _philSysRepository.DeleteTransactionDataAsync(HashToken);
	}

	public async Task<TransactionStatusResponse> GetLivenessSessionStatusAsync(string HashToken)
	{
		var cacheKey = $"PhilSys_LivenessSessionStatus_{HashToken}";

		return await _hybridCache.GetOrCreateAsync<TransactionStatusResponse>(
			cacheKey,
			async status => await _philSysRepository.GetLivenessSessionStatusAsync(HashToken));
	}

	public async Task<PhilSysTransaction> GetTransactionDataByHashTokenAsync(string HashToken)
	{
		return await _philSysRepository.GetTransactionDataByHashTokenAsync(HashToken);
	}

	public async Task<PhilSysTransaction> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSessionId, byte[] Photo)
	{
		return await _philSysRepository.UpdateFaceLivenessSessionAsync(HashToken, FaceLivenessSessionId);
	}

	public Task<PhilSysTransaction> UpdateTransactionDataAsync(PhilSysTransaction Transaction)
	{
		return _philSysRepository.UpdateTransactionDataAsync(Transaction);
	}
}
