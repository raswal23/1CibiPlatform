namespace ATS.BackgroundJobs.FollowUpEmail;

/// <summary>
/// Requeues the application form invitation for orders whose package follow-up interval has
/// elapsed with the form still unanswered.
/// </summary>
/// <remarks>
/// This job QUEUES and never sends. Releasing a row back to Pending hands it to
/// <c>EmailNotificationBackgroundJob</c>, which carries it through the same per-account pool,
/// daily cap and send pacing as a first invitation - so a burst of reminders can never
/// outrun the quota the way a direct SMTP call from here would.
/// </remarks>
[DisallowConcurrentExecution]
public class FollowUpEmailBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<FollowUpEmailBackgroundJob> _logger;

	public FollowUpEmailBackgroundJob(IServiceScopeFactory scopeFactory, ILogger<FollowUpEmailBackgroundJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		using var scope = _scopeFactory.CreateScope();

		var endorsementSubmissionService = scope.ServiceProvider
			.GetRequiredService<IEndorsementSubmissionService>();

		// Jobs catch; feature code does not. An unhandled throw here would let Quartz retry
		// the misfire, and the next hourly pass picks up whatever this one missed anyway -
		// the fire-once stamp means a failed pass releases nothing rather than double-sending.
		try
		{
			await endorsementSubmissionService.ReleaseDueFollowUpEmailsAsync(context.CancellationToken);
		}
		catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
		{
			// Shutdown, not a fault.
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to release due application form follow-up reminders.");
		}
	}
}
