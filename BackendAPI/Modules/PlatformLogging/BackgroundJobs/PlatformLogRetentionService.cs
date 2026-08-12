using PlatformLogging.Configuration;

namespace PlatformLogging.BackgroundJobs;

public sealed class PlatformLogRetentionService(
	IServiceScopeFactory scopeFactory,
	IOptions<PlatformLoggingOptions> options,
	ILogger<PlatformLogRetentionService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!options.Value.PostgreSqlEnabled || !options.Value.RetentionEnabled)
		{
			return;
		}

		var retentionInterval = TimeSpan.FromHours(
			Math.Max(1, options.Value.RetentionIntervalHours));
		using var timer = new PeriodicTimer(retentionInterval);

		do
		{
			try
			{
				int deleted;
				do
				{
					using var scope = scopeFactory.CreateScope();
					var repository = scope.ServiceProvider.GetRequiredService<IPlatformLogRepository>();
					deleted = await repository.DeleteExpiredBatchAsync(stoppingToken);
				}
				while (deleted >= Math.Max(100, options.Value.RetentionBatchSize)
					&& !stoppingToken.IsCancellationRequested);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Platform log retention failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
