
namespace PhilSys.Data.Cache;

public class PhilSysCacheRepository : IPhilSysRepository
{
	private readonly IPhilSysRepository philSysRepository;
	private readonly HybridCache hybridCache;

	public PhilSysCacheRepository(IPhilSysRepository philSysRepository, HybridCache hybridCache)
	{
		this.philSysRepository = philSysRepository;
		this.hybridCache = hybridCache;
	}

	public async Task<bool> AddTransactionDataAsync(PhilSysTransaction PhilSysTransaction)
	{
		return await philSysRepository.AddTransactionDataAsync(PhilSysTransaction);
	}

	public async Task<bool> DeleteTransactionDataAsync(PhilSysTransaction HashToken)
	{
		return await philSysRepository.DeleteTransactionDataAsync(HashToken);
	}

	public async Task<TransactionStatusResponse> GetLivenessSessionStatusAsync(string HashToken)
	{
		var cacheKey = $"PhilSys_LivenessSessionStatus_{HashToken}";

		return await hybridCache.GetOrCreateAsync<TransactionStatusResponse>(
			cacheKey,
			async status => await philSysRepository.GetLivenessSessionStatusAsync(HashToken));
	}

	public async Task<PhilSysTransaction> GetTransactionDataByHashTokenAsync(string HashToken)
	{
		return await philSysRepository.GetTransactionDataByHashTokenAsync(HashToken);
	}

	public async Task<PhilSysTransaction> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSessionId, byte[] Photo)
	{
		return await philSysRepository.UpdateFaceLivenessSessionAsync(HashToken, FaceLivenessSessionId, Photo);
	}

	public Task<PhilSysTransaction> UpdateTransactionDataAsync(PhilSysTransaction transaction)
	{
		return philSysRepository.UpdateTransactionDataAsync(transaction);
	}
}
