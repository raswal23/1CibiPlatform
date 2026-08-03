namespace ATS.BackgroundJobs.EmailNotification;

public class EmailNotificationBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;

	public EmailNotificationBackgroundJob(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IEmailNotificationProcessorService>();

		await processor.ProcessForPendingStatusAsync(context.CancellationToken);
	}
}

