namespace EmploymentVerification.BackgroundJobs.AutoVerificationRequest;

/// <summary>
/// Sends employment verification requests for application forms that have been
/// submitted, without waiting for an operator to trigger each one.
/// </summary>
/// <remarks>
/// This job decides nothing. Eligibility, consent, recipient resolution and sending all
/// live in <see cref="IAutoVerificationRequestService"/>; what is here is the schedule,
/// the scope, and the catch.
/// <para>
/// Disabled unless <c>EmailVerification:AutoSendEnabled</c> is true. Turning sending on
/// is the moment real former employers start receiving mail, so it ships off and is
/// enabled deliberately per environment rather than by deploying.
/// </para>
/// </remarks>
[DisallowConcurrentExecution]
public class AutoVerificationRequestJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IConfiguration _configuration;
	private readonly ILogger<AutoVerificationRequestJob> _logger;

	public AutoVerificationRequestJob(
		IServiceScopeFactory scopeFactory,
		IConfiguration configuration,
		ILogger<AutoVerificationRequestJob> logger)
	{
		_scopeFactory = scopeFactory;
		_configuration = configuration;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(
			new Dictionary<string, object> { ["Application"] = "EmploymentVerification" });

		var enabled = _configuration
			.GetSection("EmailVerification")
			.GetValue("AutoSendEnabled", false);

		if (!enabled)
		{
			return;
		}

		using var scope = _scopeFactory.CreateScope();

		var autoRequestService = scope.ServiceProvider
			.GetRequiredService<IAutoVerificationRequestService>();

		// Jobs catch; feature code does not. The service already catches per segment so
		// one bad address cannot end a pass - this is the outer net for anything that
		// fails the whole pass, such as the ATS read. Quartz would otherwise retry the
		// misfire, and the next tick picks up the same work anyway.
		try
		{
			var result = await autoRequestService.SendDueRequestsAsync(context.CancellationToken);

			if (result.Sent > 0 || result.Failed > 0)
			{
				_logger.LogInformation(
					"Employment verification auto-send: {Sent} sent, {Failed} failed, "
					+ "{NoConsent} without consent, {NoRecipient} without a recipient, "
					+ "of {Eligible} eligible segments.",
					result.Sent,
					result.Failed,
					result.SkippedNoConsent,
					result.SkippedNoRecipient,
					result.Eligible);
			}
		}
		catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
		{
			// Shutdown, not a fault.
		}
		catch (Exception exception)
		{
			_logger.LogError(
				exception,
				"Employment verification auto-send pass failed.");
		}
	}
}
