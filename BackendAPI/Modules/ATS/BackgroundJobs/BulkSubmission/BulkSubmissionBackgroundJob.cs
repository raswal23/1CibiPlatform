namespace ATS.BackgroundJobs.BulkSubmission;

[DisallowConcurrentExecution]
public class BulkSubmissionBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;

	private readonly ILogger<BulkSubmissionBackgroundJob> _logger;

	public BulkSubmissionBackgroundJob(IServiceScopeFactory scopeFactory, ILogger<BulkSubmissionBackgroundJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IBulkSubmissionProcessorService>();

		await processor.ProcessAsync(context.CancellationToken);
	}
}
