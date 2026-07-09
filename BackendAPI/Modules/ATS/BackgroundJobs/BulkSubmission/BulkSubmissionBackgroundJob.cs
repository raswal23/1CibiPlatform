namespace ATS.BackgroundJobs.BulkSubmission;

public class BulkSubmissionBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;

	public BulkSubmissionBackgroundJob(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IBulkSubmissionProcessorService>();

		await processor.ProcessAsync(context.CancellationToken);
	}
}
