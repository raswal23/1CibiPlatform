namespace ATS.BackgroundServices;

public class EmailNotificationBackgroundServiceForError : BackgroundService
{
	private readonly IConfiguration _configuration;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly HybridCache _hybridCache;
	private readonly ILogger<EmailNotificationBackgroundServiceForError> _logger;
	private readonly IConnectionMultiplexer _redis;
	private readonly string _batchesError;
	private readonly string _applicationformBaseUrl;

	public EmailNotificationBackgroundServiceForError(
		IConfiguration configuration,
		IServiceScopeFactory scopeFactory,
		HybridCache hybridCache,
		ILogger<EmailNotificationBackgroundServiceForError> logger,
		IConnectionMultiplexer redis)
	{
		_configuration = configuration;
		_scopeFactory = scopeFactory;
		_hybridCache = hybridCache;
		_logger = logger;
		_redis = redis;
		_batchesError = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesError") ?? string.Empty;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;

	}
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			var dbRedis = _redis.GetDatabase();

			using var scope = _scopeFactory.CreateScope();

			var sendEmail = scope.ServiceProvider
				.GetRequiredService<IEndorsementSubmissionService>();

			var repository = scope.ServiceProvider
				.GetRequiredService<IATSRepository>();

			var cacheKey = await dbRedis.ListLeftPopAsync(_batchesError);


			if (string.IsNullOrEmpty(cacheKey))
			{
				await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
				continue;
			}

			var cached = await _hybridCache.GetOrCreateAsync(
				cacheKey!,
				async entry =>
				{
					return new List<EmailInvitationRequest>();
				});

			List<EmailInvitationRequest> successList = new();

			foreach (var request in cached)
			{
				var logContext = new
				{
					Action = "ApplicationFormEmailSendingForDeadLetter",
					Step = "SendEmail",
					Identity = request.EmailInvitationID,
					Timestamp = DateTime.UtcNow
				};

				try
				{
					var subjectName = $"{request.FirstName} {request.LastName}";

					await sendEmail.SendApplicationFormToUserEmailAsync(
						request.EmailAddress!,
						subjectName,
						_applicationformBaseUrl);

					successList.Add(request);
				}

				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to send email to {Email}: {@Context}", request.EmailAddress, logContext);
				}
			}

			_logger.LogInformation("Email processing completed. Success: {SuccessCount}", successList.Count);

			if (successList.Any())
			{
				await repository.UpdateEmailInvitationRequestForSentEmailAsync(successList);
			}

			await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
		}
	}
}
