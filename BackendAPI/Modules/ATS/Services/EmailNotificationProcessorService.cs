namespace ATS.Services;

public class EmailNotificationProcessorService : IEmailNotificationProcessorService
{
	private readonly ILogger<EmailNotificationProcessorService> _logger;
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;
	private readonly IATSRepository _repository;
	private readonly IConfiguration _configuration;
	private readonly IConnectionMultiplexer _redis;
	private readonly HybridCache _hybridCache;
	private readonly string _applicationformBaseUrl;
	private readonly string _batchesPending;
	private readonly string _batchesError;

	public EmailNotificationProcessorService(
		ILogger<EmailNotificationProcessorService> logger,
		IEndorsementSubmissionService endorsementSubmissionService,
		IATSRepository repository,
		IConfiguration configuration,
		IConnectionMultiplexer redis,
		HybridCache hybridCache)
	{
		_logger = logger;
		_endorsementSubmissionService = endorsementSubmissionService;
		_repository = repository;
		_redis = redis;
		_hybridCache = hybridCache;
		_configuration = configuration;
		_batchesPending = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesPending") ?? string.Empty;
		_batchesError = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesError") ?? string.Empty;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
	}

	public async Task ProcessForPendingStatusAsync(CancellationToken cancellationToken)
	{
		string? cacheKey = string.Empty;

		var dbRedis = _redis.GetDatabase();

		try
		{
			cacheKey = await dbRedis.ListLeftPopAsync(_batchesPending);

			if (string.IsNullOrEmpty(cacheKey))
			{
				return;
			}
		}
		catch (RedisTimeoutException ex)
		{
			_logger.LogWarning(ex, "Redis timeout while reading {_batchesPending}", _batchesPending);

			return;
		}

		var cached = await _hybridCache.GetOrCreateAsync(
			cacheKey!,
			async entry =>
			{
				return new List<List<EmailInvitationRequest>>();
			});

		var allRequests = cached.SelectMany(x => x).ToList();

		List<EmailInvitationRequest> successList = new();
		List<EmailInvitationRequest> errorList = new();

		foreach (var request in allRequests)
		{
			var logContext = new
			{
				Action = "ApplicationFormEmailSending",
				Step = "SendEmail",
				Identity = request.EmailInvitationID,
				Timestamp = DateTime.UtcNow
			};

			try
			{
				if (string.IsNullOrWhiteSpace(request.EmailAddress))
				{
					errorList.Add(request);
					continue;
				}

				var subjectName = $"{request.FirstName} {request.LastName}";
				var applicationFormLink = $"{_applicationformBaseUrl}/{request.HashToken}";

				await _endorsementSubmissionService.SendApplicationFormToUserEmailAsync(
					request.EmailAddress,
					subjectName,
					applicationFormLink);

				successList.Add(request);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send email to {Email}: {@Context}", request.EmailAddress, logContext);

				errorList.Add(request);
			}
		}

		if (errorList.Any())
		{
			const int maxRetries = 3;

			for (int retry = 1; retry <= maxRetries && errorList.Any(); retry++)
			{
				var failedItems = errorList.ToList();

				errorList.Clear();

				foreach (var request in failedItems)
				{
					var logContext = new
					{
						Action = "RetryApplicationFormEmailSending",
						Step = "SendEmail",
						Identity = request.EmailInvitationID,
						Timestamp = DateTime.UtcNow
					};

					try
					{
						if (string.IsNullOrWhiteSpace(request.EmailAddress))
						{
							errorList.Add(request);
							continue;
						}

						var subjectName = $"{request.FirstName} {request.LastName}";

						await _endorsementSubmissionService.SendApplicationFormToUserEmailAsync(
							request.EmailAddress,
							subjectName,
							_applicationformBaseUrl);

						successList.Add(request);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Retry {Retry} failed for {Email}: {@Context}", retry, request.EmailAddress, logContext);

						errorList.Add(request);
					}
				}
			}
		}

		_logger.LogInformation(
			"Email processing completed. Success: {SuccessCount}, Failed: {FailedCount}",
			successList.Count,
			errorList.Count);

		if (successList.Any())
		{
			await _repository.UpdateBulkEmailInvitationRequestForSentEmailAsync(successList);
		}

		if (errorList.Any())
		{
			var batchId = $"batch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

			await _repository.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(errorList);

			await _hybridCache.SetAsync(
					batchId,
					errorList,
					new HybridCacheEntryOptions
					{
						Expiration = TimeSpan.FromMinutes(30)
					});

			await dbRedis.ListRightPushAsync(_batchesError, batchId);
		}

		await _hybridCache.RemoveAsync(cacheKey!);
	}

	public async Task ProcessForErrorStatusAsync(CancellationToken cancellationToken)
	{
		string? cacheKey = string.Empty;

		var dbRedis = _redis.GetDatabase();

		try
		{
			cacheKey = await dbRedis.ListLeftPopAsync(_batchesError);

			if (string.IsNullOrEmpty(cacheKey))
			{
				return;
			}
		}
		catch (RedisTimeoutException ex)
		{
			_logger.LogWarning(ex, "Redis timeout while reading {_batchesError}", _batchesError);

			return;
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

				await _endorsementSubmissionService.SendApplicationFormToUserEmailAsync(
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
			await _repository.UpdateBulkEmailInvitationRequestForSentEmailAsync(successList);
		}
	}
}
