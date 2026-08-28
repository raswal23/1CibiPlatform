namespace ATS.Services.EmailNotificationProcessor;

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
	private readonly string _applicationformBaseUrl;

	// Comfortably longer than a full send pass (100 messages, 3 retries each) so a live
	// worker is never robbed of rows it is still processing.
	private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(30);

	public EmailNotificationProcessorService(
		ILogger<EmailNotificationProcessorService> logger,
		IEndorsementSubmissionService endorsementSubmissionService,
		IATSRepository repository,
		IConfiguration configuration)
	{
		_logger = logger;
		_endorsementSubmissionService = endorsementSubmissionService;
		_repository = repository;
		_configuration = configuration;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
	}

	public async Task ProcessForPendingStatusAsync(CancellationToken cancellationToken)
	{
		// A crash mid-send leaves rows claimed as Processing with no live worker, so
		// release anything stale before claiming the next slice.
		var released = await _repository.ReleaseStaleEmailInvitationClaimsAsync(StaleClaimTimeout);

		if (released > 0)
		{
			_logger.LogWarning(
				"Released {ReleasedCount} stale email invitation claim(s) back to Pending.",
				released);
		}

		// PostgreSQL is the queue: the claim atomically moves a slice of Pending rows to
		// Processing, so a concurrent worker cannot pick up the same invitations.
		var allRequests = await _repository.GetPendingEmailInvitationRequestsAsync();

		if (allRequests.Count == 0)
		{
			return;
		}

		List<EmailInvitationRequest> successList = new();
		List<EmailInvitationRequest> errorList = new();

		foreach (var request in allRequests)
		{
			if (await TrySendEmailWithRetryAsync(request))
			{
				successList.Add(request);
			}
			else
			{
				errorList.Add(request);
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
			await _repository.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(errorList);
		}
	}


	private async Task<bool> TrySendEmailWithRetryAsync(EmailInvitationRequest request)
	{
		const int maxAttempts = 3;

		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			if (await TrySendEmailAsync(request, attempt == 1 ? null : attempt))
			{
				return true;
			}
		}

		return false;
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
				applicationFormLink,
				request.Requestor,
				request.ClientId);

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
