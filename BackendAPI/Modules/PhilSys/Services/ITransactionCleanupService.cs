namespace PhilSys.Services;

public interface ITransactionCleanupService
{
	Task CleanupExpiredTransactionsAsync(CancellationToken cancellationToken = default);
}
