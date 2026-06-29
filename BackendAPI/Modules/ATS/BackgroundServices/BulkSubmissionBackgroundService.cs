namespace ATS.BackgroundServices;

public class BulkSubmissionBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;

	public BulkSubmissionBackgroundService(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{

			using var scope = _scopeFactory.CreateScope();

			var processor = scope.ServiceProvider
				.GetRequiredService<IBulkSubmissionProcessorService>();

			await processor.ProcessAsync(stoppingToken);

			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}
}
