namespace ATS.BackgroundServices;

public class EmailNotificationBackgroundServiceForPending : BackgroundService
{
	private readonly ILogger<EmailNotificationBackgroundServiceForPending> _logger;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IConfiguration _configuration;
	private readonly IConnectionMultiplexer _redis;
	private readonly HybridCache _hybridCache;
	private readonly string _applicationformBaseUrl;
	private readonly string _batchesPending;
	private readonly string _batchesError;


	public EmailNotificationBackgroundServiceForPending(
		ILogger<EmailNotificationBackgroundServiceForPending> logger,
		IServiceScopeFactory scopeFactory, 
		IConfiguration configuration,
		IConnectionMultiplexer redis,
		HybridCache hybridCache)
	{
		_logger = logger;
		_scopeFactory = scopeFactory;
		_configuration = configuration;
		_redis = redis;
		_batchesPending = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesPending") ?? string.Empty;
		_batchesError = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesError") ?? string.Empty;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
		_hybridCache = hybridCache;
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

			var cacheKey = await dbRedis.ListLeftPopAsync(_batchesPending);

			var cached = await _hybridCache.GetOrCreateAsync(
				cacheKey!,
				async entry =>
				{
					return new List<List<EmailInvitationRequest>>();
				});

			var allRequests = cached.SelectMany(x => x).ToList();

			List<EmailInvitationRequest> successList = new();
			List<EmailInvitationRequest> errorList = new();

			if (!allRequests.Any())
			{
				await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
				continue;
			}

			foreach (var request in allRequests)
			{
				try
				{
					if (string.IsNullOrWhiteSpace(request.EmailAddress))
					{
						errorList.Add(request);
						continue;
					}

					var subjectName = $"{request.FirstName} {request.LastName}";

					await sendEmail.SendApplicationFormToUserEmailAsync(
						request.EmailAddress,
						subjectName,
						_applicationformBaseUrl);

					successList.Add(request);
				}
				catch (Exception ex)
				{
					_logger.LogError(
						ex,
						"Failed to send email to {Email}",
						request.EmailAddress);

					errorList.Add(request);
				}
			}

			const int maxRetries = 3;

			for (int retry = 1; retry <= maxRetries && errorList.Any(); retry++)
			{
				_logger.LogInformation(
					"Retry round {Retry}. Failed emails count: {Count}",
					retry,
					errorList.Count);

				var failedItems = errorList.ToList();

				errorList.Clear();

				foreach (var request in failedItems)
				{
					try
					{
						if (string.IsNullOrWhiteSpace(request.EmailAddress))
						{
							errorList.Add(request);
							continue;
						}

						var subjectName = $"{request.FirstName} {request.LastName}";

						await sendEmail.SendApplicationFormToUserEmailAsync(
							request.EmailAddress,
							subjectName,
							_applicationformBaseUrl);

						successList.Add(request);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(
							ex,
							"Retry {Retry} failed for {Email}",
							retry,
							request.EmailAddress);

						errorList.Add(request);
					}
				}

				if (errorList.Any())
				{
					await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
				}
			}

			_logger.LogInformation(
				"Email processing completed. Success: {SuccessCount}, Failed: {FailedCount}",
				successList.Count,
				errorList.Count);

			if (successList.Any())
			{
				await repository.UpdateEmailInvitationRequestForSuccessAsync(successList);
			}

			var batchId = $"batch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

			if (errorList.Any())
			{
				await repository.UpdateEmailInvitationRequestForErrorAsync(errorList);

				await _hybridCache.SetAsync(
						batchId,
						errorList,
						new HybridCacheEntryOptions
						{
							Expiration = TimeSpan.FromMinutes(30)
						});
					}

				await dbRedis.ListRightPushAsync(_batchesError, batchId);

			//await _hybridCache.RemoveAsync(CacheKeys.ATSCacheKeys.BulkSubjectsCacheKey);

			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}
}

