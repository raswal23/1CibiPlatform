namespace ATS.BackgroundServices;

public class EmailNotificationBackgroundServiceForPending : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;

	public EmailNotificationBackgroundServiceForPending(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			using var scope = _scopeFactory.CreateScope();

			var processor = scope.ServiceProvider
				.GetRequiredService<IEmailNotificationProcessorService>();

			await processor.ProcessForPendingStatusAsync(stoppingToken);

			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}
}

