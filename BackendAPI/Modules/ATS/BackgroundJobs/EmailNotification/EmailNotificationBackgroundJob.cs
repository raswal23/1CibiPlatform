namespace ATS.BackgroundJobs.EmailNotification;

public class EmailNotificationBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<EmailNotificationBackgroundJob> _logger;

	public EmailNotificationBackgroundJob(IServiceScopeFactory scopeFactory, ILogger<EmailNotificationBackgroundJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IEmailNotificationProcessorService>();

		await processor.ProcessForPendingStatusAsync(context.CancellationToken);
	}
}
