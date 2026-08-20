namespace ATS.Services;

public class EmailNotificationProcessorService : IEmailNotificationProcessorService
{
	private readonly ILogger<EmailNotificationProcessorService> _logger;
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;
	private readonly IATSRepository _repository;
	private readonly IConfiguration _configuration;
	private readonly string _applicationformBaseUrl;

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
		// PostgreSQL is the queue: rows still marked Pending are the work item, so a
		// restart mid-batch simply re-reads them on the next tick.
		var allRequests = await _repository.GetPendingEmailInvitationRequestsAsync();

		if (allRequests.Count == 0)
		{
			return;
		}

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
