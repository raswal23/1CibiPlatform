namespace ATS.Services;

public class EmailNotificationProcessorService : IEmailNotificationProcessorService
{
	// Atomically claims the oldest pending batch: ZPOPMIN from pending, ZADD into
	// processing with the claim timestamp as score. Atomic because Quartz runs
	// clustered, so two nodes may poll at the same time.
	private const string ClaimBatchScript = @"
		local popped = redis.call('ZPOPMIN', KEYS[1])
		if #popped == 0 then return nil end
		redis.call('ZADD', KEYS[2], ARGV[1], popped[1])
		return popped[1]";

	private readonly ILogger<EmailNotificationProcessorService> _logger;
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;
	private readonly IATSRepository _repository;
	private readonly IConfiguration _configuration;
	private readonly IConnectionMultiplexer _redis;
	private readonly string _applicationformBaseUrl;
	private readonly string _batchesPending;
	private readonly string _batchesProcessing;

	public EmailNotificationProcessorService(
		ILogger<EmailNotificationProcessorService> logger,
		IEndorsementSubmissionService endorsementSubmissionService,
		IATSRepository repository,
		IConfiguration configuration,
		IConnectionMultiplexer redis)
	{
		_logger = logger;
		_endorsementSubmissionService = endorsementSubmissionService;
		_repository = repository;
		_redis = redis;
		_configuration = configuration;
		_batchesPending = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesPending") ?? string.Empty;
		_batchesProcessing = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesProcessing") ?? string.Empty;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
	}

	public async Task ProcessForPendingStatusAsync(CancellationToken cancellationToken)
	{
		string? cacheKey;

		var dbRedis = _redis.GetDatabase();

		try
		{
			var claimed = await dbRedis.ScriptEvaluateAsync(
				ClaimBatchScript,
				[(RedisKey)_batchesPending, (RedisKey)_batchesProcessing],
				[(RedisValue)DateTimeOffset.UtcNow.ToUnixTimeSeconds()]);

			cacheKey = claimed.IsNull ? null : (string?)(RedisValue)claimed;

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

		var payload = await dbRedis.StringGetAsync(cacheKey);

		if (payload.IsNullOrEmpty)
		{
			_logger.LogWarning(
				"Batch payload missing or expired for {BatchId}; dropping batch.",
				cacheKey);

			await dbRedis.SortedSetRemoveAsync(_batchesProcessing, cacheKey);

			return;
		}

		var cached = JsonSerializer.Deserialize<List<List<EmailInvitationRequest>>>((string)payload!)
			?? new List<List<EmailInvitationRequest>>();

		var allRequests = cached.SelectMany(x => x).ToList();

		List<EmailInvitationRequest> successList = new();
		List<EmailInvitationRequest> errorList = new();

		foreach (var request in allRequests)
		{
			if (await TrySendEmailAsync(request))
			{
				successList.Add(request);
			}
			else
			{
				errorList.Add(request);
			}
		}

		if (errorList.Any())
		{
			await RetryFailedEmailsAsync(successList, errorList);
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
			await _repository.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(errorList);
		}

		await dbRedis.KeyDeleteAsync(cacheKey);

		await dbRedis.SortedSetRemoveAsync(
			_batchesProcessing,
			cacheKey);
	}


	private async Task RetryFailedEmailsAsync(
		List<EmailInvitationRequest> successList,
		List<EmailInvitationRequest> errorList)
	{
		const int maxRetries = 3;

		for (int retry = 1; retry <= maxRetries && errorList.Any(); retry++)
		{
			var failedItems = errorList.ToList();

			errorList.Clear();

			foreach (var request in failedItems)
			{
				if (await TrySendEmailAsync(request))
				{
					successList.Add(request);
				}
				else
				{
					errorList.Add(request);
				}
			}
		}
	}

	private async Task<bool> TrySendEmailAsync(
	EmailInvitationRequest request,
	int? retry = null)
	{
		var logContext = new
		{
			Action = retry is null
				? "ApplicationFormEmailSending"
				: "RetryApplicationFormEmailSending",
			Step = "SendEmail",
			Identity = request.EmailInvitationID,
			Timestamp = DateTime.UtcNow
		};

		try
		{
			if (string.IsNullOrWhiteSpace(request.EmailAddress))
			{
				return false;
			}

			var subjectName = $"{request.FirstName} {request.LastName}";
			var applicationFormLink = $"{_applicationformBaseUrl}/{request.HashToken}";

			await _endorsementSubmissionService.SendApplicationFormToUserEmailAsync(
				request.EmailAddress,
				subjectName,
				applicationFormLink);

			return true;
		}
		catch (Exception ex)
		{
			if (retry is null)
			{
				_logger.LogError(
					ex,
					"Failed to send email to {Email}: {@Context}",
					request.EmailAddress,
					logContext);
			}
			else
			{
				_logger.LogError(
					ex,
					"Retry {Retry} failed for {Email}: {@Context}",
					retry,
					request.EmailAddress,
					logContext);
			}

			return false;
		}
	}
}
