namespace PhilSys.Services;

public interface ILivenessSessionService
{
	Task<TransactionStatusResponseDTO> IsLivenessUsedAsync(string HashToken);
}
