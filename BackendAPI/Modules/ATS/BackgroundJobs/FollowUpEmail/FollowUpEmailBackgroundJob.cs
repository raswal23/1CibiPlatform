namespace ATS.BackgroundJobs.FollowUpEmail;

/// <summary>
/// Requeues the application form invitation for orders that are due a daily follow-up
/// reminder with the form still unanswered.
/// </summary>
/// <remarks>
/// This job QUEUES and never sends. Releasing a row back to Pending hands it to
/// <c>EmailNotificationBackgroundJob</c>, which carries it through the same per-account pool,
/// daily cap and send pacing as a first invitation - so a burst of reminders can never
/// outrun the quota the way a direct SMTP call from here would. That matters more now than
/// it did under the fire-once design: a package set to N sends up to N reminders per
/// unanswered order, so the volume this can release scales with the setting.
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
		// the release and its date stamp share one statement, so a failed pass releases
		// nothing rather than sending a second reminder on the same day.
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
