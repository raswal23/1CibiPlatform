using ATS.Configuration;

namespace ATS.BackgroundJobs.Notifications;

/// <summary>
/// Deletes send-log rows past the retention window. Modelled on
/// AtsNotificationRetentionService, which solves the same problem for the bell.
/// </summary>
/// <remarks>
/// The send log exists to answer one question - "how many recipients has this account taken in
/// the last 24 hours" - and that query never looks further back than the quota window. Without a
/// sweep the table grows by one row per email forever to serve a read that ignores all but the
/// last day of it.
///
/// The retention window is deliberately wider than the quota window: trimming at exactly 24
/// hours would race the counting query and could subtract consumption an account has genuinely
/// used, which reads as free capacity and walks straight into the provider's cap.
/// </remarks>
public sealed class AtsEmailSendLogRetentionService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly AtsEmailDeliveryOptions _options;
	private readonly ILogger<AtsEmailSendLogRetentionService> _logger;

	public AtsEmailSendLogRetentionService(
		IServiceScopeFactory scopeFactory,
		IOptions<AtsEmailDeliveryOptions> options,
		ILogger<AtsEmailSendLogRetentionService> logger)
	{
		_scopeFactory = scopeFactory;
		_options = options.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Hourly. The rows are small and the cutoff moves continuously, so a long interval
		// would only ever mean a bigger delete doing identical work.
		using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

		do
		{
			try
			{
				await SweepAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				// Swallowed on purpose: a failed sweep costs disk, while an escaping exception
				// from a BackgroundService takes the host down and stops every email with it.
				_logger.LogError(exception, "ATS email send log retention failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}

	private async Task SweepAsync(CancellationToken cancellationToken)
	{
		var retentionHours = Math.Max(
			_options.QuotaWindowHours + 1,
			_options.SendLogRetentionHours);

		var cutoff = DateTime.UtcNow.AddHours(-retentionHours);

		using var scope = _scopeFactory.CreateScope();

		var repository = scope.ServiceProvider.GetRequiredService<IAtsEmailAccountRepository>();

		var deleted = await repository.DeleteSendLogsOlderThanAsync(cutoff, cancellationToken);

		if (deleted > 0)
		{
			_logger.LogInformation(
				"Deleted {Count} ATS email send log rows older than {Cutoff}",
				deleted,
				cutoff);
		}
	}
}
