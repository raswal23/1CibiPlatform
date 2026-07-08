namespace ATS.BackgroundServices;

public class BulkSubmissionJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;

	public BulkSubmissionJob(IServiceScopeFactory scopeFactory)
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
