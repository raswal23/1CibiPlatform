namespace ATS.BackgroundJobs.EmailNotificationRecovery;

[DisallowConcurrentExecution]
public class EmailNotificationRecoveryJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<EmailNotificationRecoveryJob> _logger;

	public EmailNotificationRecoveryJob(IServiceScopeFactory scopeFactory, ILogger<EmailNotificationRecoveryJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var service = scope.ServiceProvider.GetRequiredService<IEmailNotificationRecoveryService>();
			await service.RequeueStaleBatchesAsync(context.CancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "EmailNotificationRecoveryJob failed.");
		}
	}
}
