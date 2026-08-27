namespace ATS.BackgroundJobs.OMSTicketing;

[DisallowConcurrentExecution]
public class OMSTicketingBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;

	private readonly ILogger<OMSTicketingBackgroundJob> _logger;

	public OMSTicketingBackgroundJob(IServiceScopeFactory scopeFactory, ILogger<OMSTicketingBackgroundJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IOMSTicketingProcessorService>();

		await processor.ProcessAsync(context.CancellationToken);
	}
}
