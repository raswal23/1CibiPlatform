namespace ATS.BackgroundJobs.ApplicantSearchProjection;

[DisallowConcurrentExecution]
public class ApplicantSearchProjectionJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<ApplicantSearchProjectionJob> _logger;

	public ApplicantSearchProjectionJob(IServiceScopeFactory scopeFactory, ILogger<ApplicantSearchProjectionJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var service = scope.ServiceProvider.GetRequiredService<IApplicantSearchProjectionService>();
			await service.ProcessPendingProjectionsAsync(context.CancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "ApplicantSearchProjectionJob failed.");
		}
	}
}
