namespace PhilSys.BackgroundJobs.TransactionCleanup;

[DisallowConcurrentExecution]
public class PhilSysTransactionCleanupJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<PhilSysTransactionCleanupJob> _logger;

	public PhilSysTransactionCleanupJob(IServiceScopeFactory scopeFactory, ILogger<PhilSysTransactionCleanupJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "PhilSys" });
		using var scope = _scopeFactory.CreateScope();

		var cleanupService = scope.ServiceProvider
			.GetRequiredService<ITransactionCleanupService>();

		await cleanupService.CleanupExpiredTransactionsAsync(context.CancellationToken);
	}
}
