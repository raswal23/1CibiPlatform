namespace ATS.Services;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly ILogger<DisputeOrderService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;
	private readonly string _disputeOrderEmailRecipient;

	public DisputeOrderService(
		ILogger<DisputeOrderService> logger,
		[FromKeyedServices("ats")] IEmailService emailService,
		IConfiguration configuration,
		IATSRepository atsRepository,
		IHttpContextAccessor httpContextAccessor)
	{
		_logger = logger;
		_emailService = emailService;
		_configuration = configuration;
		_disputeOrderEmailRecipient = _configuration.GetSection("ATS").GetValue<string>("DisputeOrderEmailRecipient", "");
		_atsRepository = atsRepository;
		_httpContextAccessor = httpContextAccessor;
	}

	public Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetDisputeOrders",
			Step = "FetchingDisputeOrders",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching dispute orders with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
				_atsRepository.GetDisputeOrdersAsync(paginationRequest, cancellationToken) :
				_atsRepository.SearchDisputeOrdersAsync(paginationRequest, cancellationToken);
	}

	public async Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "MarkAsDisputed",
			Step = "UpdatingDisputeStatus",
			EmailInvitationId = disputeRequest.EmailInvitationId,
			Timestamp = DateTime.UtcNow
		};

		var requestor = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value ?? 
					_httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;

		_logger.LogInformation("Marking order as disputed: {@Context}", logContext);

		try
		{
			await SendDisputeOrderEmailAsync(_disputeOrderEmailRecipient, disputeRequest, requestor!);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to send dispute order notification email. {@Context}",
				logContext);

			throw new InternalServerException("Failed to send dispute order notification email.");
		}

		try
		{
			await _atsRepository.MarkAsDisputedAsync(disputeRequest, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to mark order as disputed. {@Context}",
				logContext);

			throw new InternalServerException("Failed to mark order as disputed.");
		}

		return true;
	}

	public async Task<bool> SendDisputeOrderEmailAsync(string gmail, DisputeOrderRequestDTO disputeRequest, string requestor)
	{
		var logContext = new
		{
			Action = "SendDisputeOrderEmail",
			Step = "SendEmail",
			Email = gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending dispute order notification for email: {@Context}", logContext);

		var otpBody = _emailService.SendEmailForDispute(gmail, disputeRequest.Company!, disputeRequest.DisputeReason!, disputeRequest.OrderCreatedAt, requestor);

		var isSent = await _emailService.SendATSEmailAsync(
			toEmail: gmail!,
			subject: "CIBI | Dispute Order Notification",
			body: otpBody
		);

		if (!isSent)
		{
			_logger.LogError("Failed to send dispute order notification email to: {@Context}", logContext);
			throw new InternalServerException("Failed to send dispute order notification email.");
		}

		return isSent;
	}
}
