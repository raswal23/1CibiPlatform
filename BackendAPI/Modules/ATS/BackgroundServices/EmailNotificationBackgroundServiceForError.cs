namespace ATS.BackgroundServices;

public class EmailNotificationBackgroundServiceForError : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;

	public EmailNotificationBackgroundServiceForError(IServiceScopeFactory scopeFactory)
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

			await processor.ProcessForErrorStatusAsync(stoppingToken);

			await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
		}
	}
}
