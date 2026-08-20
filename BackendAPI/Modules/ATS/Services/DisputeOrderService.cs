namespace ATS.Services;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly ILogger<DisputeOrderService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IClientRepository _clientRepository;
	private readonly AtsQueryScopeResolver _scopeResolver;
	private readonly ICurrentUser _currentUser;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;
	private readonly string _disputeOrderEmailRecipient;
	private readonly IOrderHistoryService _orderHistoryService;

	public DisputeOrderService(
		ILogger<DisputeOrderService> logger,
		[FromKeyedServices("ats")] IEmailService emailService,
		IConfiguration configuration,
		IATSRepository atsRepository,
		IClientRepository clientRepository,
		IHttpContextAccessor httpContextAccessor,
		AtsQueryScopeResolver scopeResolver,
		ICurrentUser currentUser,
		IOrderHistoryService orderHistoryService)
	{
		_logger = logger;
		_emailService = emailService;
		_configuration = configuration;
		_disputeOrderEmailRecipient = _configuration.GetSection("ATS").GetValue<string>("DisputeOrderEmailRecipient", "");
		_atsRepository = atsRepository;
		_clientRepository = clientRepository;
		_scopeResolver = scopeResolver;
		_currentUser = currentUser;
		_httpContextAccessor = httpContextAccessor;
		_orderHistoryService = orderHistoryService;
	}

	public async Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetDisputeOrders",
			Step = "FetchingDisputeOrders",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching dispute orders with pagination: {@Context}", logContext);
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		if (scope.Kind == AtsQueryScopeKind.Denied)
		{
			return new PaginatedResult<DisputeOrderListDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				0,
				[]);
		}

		return await (string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
				_atsRepository.GetDisputeOrdersAsync(paginationRequest, scope, cancellationToken) :
				_atsRepository.SearchDisputeOrdersAsync(paginationRequest, scope, cancellationToken));
	}

	public async Task<bool> MarkAsDisputedAsync(
		DisputeOrderRequestDTO disputeRequest,
		CancellationToken cancellationToken)
	{
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);
		if (scope.Kind == AtsQueryScopeKind.Denied)
			throw new ForbiddenException("The current user does not have access to this dispute order.");

		var logContext = new
		{
			Action = "MarkAsDisputed",
			Step = "UpdatingDisputeStatus",
			EmailInvitationId = disputeRequest.EmailInvitationId,
			Timestamp = DateTime.UtcNow
		};

		var order = await _atsRepository.GetEmailInvitationRequestByIdAsync(
			disputeRequest.EmailInvitationId,
			cancellationToken);


		if (order.EmailInvitationID == Guid.Empty)
			throw new NotFoundException("Email invitation request not found.");
		if (!IsAuthorized(order, scope))
			throw new ForbiddenException("The current user does not have access to this dispute order.");

		var clientName = await ResolveClientNameAsync(order, cancellationToken);
		if (string.IsNullOrWhiteSpace(clientName) && !_currentUser.IsPlatformSuperAdmin && _currentUser.AtsRoleId != 1)
			throw new BadRequestException("The authenticated user does not have a valid client assignment.");

		var requestor = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value ??
					_httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;
		var subjectName = string.Join(
			" ",
			new[] { order.FirstName, order.LastName }.Where(name => !string.IsNullOrWhiteSpace(name)));

		_logger.LogInformation("Marking order as disputed: {@Context}", logContext);

		try
		{
			await SendDisputeOrderEmailAsync(
				_disputeOrderEmailRecipient,
				clientName,
				disputeRequest.DisputeReason!,
				order.OrderCreatedAt,
				requestor!,
				subjectName);
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
			await _orderHistoryService.RecordAsync(
				order.EmailInvitationID,
				OrderHistoryEventType.ReportDisputed,
				order.OrderStatus,
				order.OrderStatus ?? OrderStatus.Completed, cancellationToken);
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

	private async Task<string?> ResolveClientNameAsync(
		EmailInvitationRequest order,
		CancellationToken cancellationToken)
	{
		if (order.ClientId is > 0)
		{
			return (await _clientRepository.GetClientAsync(order.ClientId.Value, cancellationToken) ?? [])
				.Select(client => client.ClientName)
				.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
		}

		return null;
	}

	private static bool IsAuthorized(EmailInvitationRequest order, AtsQueryScope scope) => scope.Kind switch
	{
		AtsQueryScopeKind.All => true,
		AtsQueryScopeKind.Client => order.ClientId == scope.ClientId,
		AtsQueryScopeKind.Clients => order.ClientId.HasValue && scope.ClientIds.Contains(order.ClientId.Value),
		AtsQueryScopeKind.ClientRequestor => order.ClientId == scope.ClientId && order.RequestorId == scope.RequestorId,
		AtsQueryScopeKind.Requestor => order.RequestorId == scope.RequestorId,
		_ => false
	};

	private async Task<bool> SendDisputeOrderEmailAsync(
		string gmail,
		string company,
		string disputeReason,
		DateTime? orderCreatedAt,
		string requestor,
		string subjectName)
	{
		var logContext = new
		{
			Action = "SendDisputeOrderEmail",
			Step = "SendEmail",
			Email = gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending dispute order notification for email: {@Context}", logContext);

		var otpBody = _emailService.SendEmailForDispute(
			gmail,
			company,
			disputeReason,
			orderCreatedAt,
			requestor,
			subjectName);

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
