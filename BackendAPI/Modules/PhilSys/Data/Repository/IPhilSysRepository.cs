namespace PhilSys.Data.Repository;
public interface IPhilSysRepository
{
	Task<bool> AddTransactionDataAsync(PhilSysTransaction PhilSysTransaction);

	Task<PhilSysTransaction> UpdateTransactionDataAsync(PhilSysTransaction transaction);

	Task<PhilSysTransaction> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSessionId, byte[] Photo);

	Task<PhilSysTransaction> GetTransactionDataByHashTokenAsync(string HashToken);

	Task<TransactionStatusResponse> GetLivenessSessionStatusAsync(string HashToken);

	Task<bool> DeleteTransactionDataAsync(PhilSysTransaction HashToken);
}
