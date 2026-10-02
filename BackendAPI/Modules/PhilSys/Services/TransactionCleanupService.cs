namespace PhilSys.Services;

public class TransactionCleanupService : ITransactionCleanupService
{
	private readonly IPhilSysRepository _philSysRepository;
	private readonly ILogger<TransactionCleanupService> _logger;
	private readonly IConfiguration _configuration;
	private readonly double _cleanupAgeMinutes;

	public TransactionCleanupService(
		IPhilSysRepository philSysRepository,
		ILogger<TransactionCleanupService> logger,
		IConfiguration configuration)
	{
		_philSysRepository = philSysRepository;
		_logger = logger;
		_configuration = configuration;
		_cleanupAgeMinutes = _configuration.GetSection("PhilSys").GetValue<double>("TransactionCleanupAgeInMinutes", 6);
	}

	public async Task CleanupExpiredTransactionsAsync(CancellationToken cancellationToken = default)
	{
		var cutoff = DateTime.UtcNow.AddMinutes(-_cleanupAgeMinutes);

		var expiredTransactions = await _philSysRepository.GetExpiredUntransactedTransactionsAsync(cutoff);

		if (expiredTransactions.Count == 0)
		{
			return;
		}

		var logContext = new
		{
			Action = "CleanupExpiredTransactions",
			Step = "StartCleanup",
			Cutoff = cutoff,
			Count = expiredTransactions.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Deleting {Count} expired untransacted PhilSys transaction(s): {@Context}", expiredTransactions.Count, logContext);

		var deletedCount = 0;

		foreach (var transaction in expiredTransactions)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var deleted = await _philSysRepository.DeleteTransactionDataAsync(transaction);

			if (!deleted)
			{
				_logger.LogError("Cleanup Failed: Failed to delete expired PhilSys Transaction with HashToken {HashToken}: {@Context}", transaction.HashToken, logContext);
				continue;
			}

			deletedCount++;
		}

		_logger.LogInformation("Successfully deleted {DeletedCount} of {Count} expired untransacted PhilSys transaction(s): {@Context}", deletedCount, expiredTransactions.Count, logContext);
	}
}
