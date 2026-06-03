namespace PhilSys.Data.Repository;
public interface IPhilSysRepository
{
	Task<bool> AddTransactionDataAsync(PhilSysTransaction PhilSysTransaction);

	Task<PhilSysTransaction> UpdateTransactionDataAsync(PhilSysTransaction Transaction);

	Task<PhilSysTransaction> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSessionId);

	Task<PhilSysTransaction> GetTransactionDataByHashTokenAsync(string HashToken);

	Task<TransactionStatusResponse> GetLivenessSessionStatusAsync(string HashToken);

	Task<bool> DeleteTransactionDataAsync(PhilSysTransaction HashToken);
	Task<bool> AddTransactionResultDataAsync(PhilSysTransactionResult PhilSysTransactionResult);

}
