namespace ATS.Services.DisputeOrder;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly ILogger<DisputeOrderService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IUserClientRepository _userClientRepository;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;
	private readonly string _disputeOrderEmailRecipient;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly ICurrentUser _currentUser;
	private readonly IAtsAccessScopeResolver _accessScopeResolver;
	private readonly IUnitOfWork _unitOfWork;

	public DisputeOrderService(
		ILogger<DisputeOrderService> logger,
		[FromKeyedServices("ats")] IEmailService emailService,
		IConfiguration configuration,
		IATSRepository atsRepository,
		IUserClientRepository userClientRepository,
		IHttpContextAccessor httpContextAccessor,
		IOrderHistoryService orderHistoryService,
		ICurrentUser currentUser,
		IAtsAccessScopeResolver accessScopeResolver,
		IUnitOfWork unitOfWork)
	{
		_accessScopeResolver = accessScopeResolver;
		_logger = logger;
		_emailService = emailService;
		_configuration = configuration;
		_disputeOrderEmailRecipient = _configuration.GetSection("ATS").GetValue<string>("DisputeOrderEmailRecipient", "");
		_atsRepository = atsRepository;
		_userClientRepository = userClientRepository;
		_httpContextAccessor = httpContextAccessor;
		_orderHistoryService = orderHistoryService;
		_currentUser = currentUser;
		_unitOfWork = unitOfWork;
	}

	public async Task<KeysetPaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetDisputeOrders",
			Step = "FetchingDisputeOrders",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching dispute orders with pagination: {@Context}", logContext);

		// The role ladder lives in AtsAccessScopeResolver now - this used to be an
		// inline copy of it.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			return CreateEmptyResult(paginationRequest);
		}

		var clientIds = scope.AuthorizedClientIds;
		var requiredRequestorId = scope.RequiredOwnerId;

		// Cursor over the fixed (createdAt, id) ordering. Both anchors are required;
		// a malformed cursor restarts the walk from the first page.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		DateTime? afterCreatedAt = DateTime.TryParse(fields?[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt) ? createdAt : null;
		Guid? afterId = Guid.TryParse(fields?[1], out var invitationId) ? invitationId : null;
		var hasSeek = afterCreatedAt.HasValue && afterId.HasValue;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _atsRepository.GetDisputeOrdersPageAsync(
			paginationRequest.SearchTerm, hasSeek ? afterCreatedAt : null, hasSeek ? afterId : null,
			pageSize + 1, clientIds, requiredRequestorId, cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		string? nextCursor = null;
		if (hasMore)
		{
			var last = items[^1];
			nextCursor = CursorCodec.Encode(
				last.OrderCreatedAt!.Value.ToString("O"),
				last.EmailInvitationID.ToString("D"));
		}

		long? totalCount = hasSeek
			? null
			: await _atsRepository.CountDisputeOrdersAsync(
				paginationRequest.SearchTerm, clientIds, requiredRequestorId, cancellationToken);

		return new KeysetPaginatedResult<DisputeOrderListDTO>(items, nextCursor, totalCount);
	}

	private static KeysetPaginatedResult<DisputeOrderListDTO> CreateEmptyResult(
		KeysetPaginationRequest paginationRequest) =>
		new(
			Array.Empty<DisputeOrderListDTO>(), null, 0);

	public async Task<bool> MarkAsDisputedAsync(
		DisputeOrderRequestDTO disputeRequest,
		Guid authenticatedUserId,
		CancellationToken cancellationToken)
	{
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

		var assignment = (await _userClientRepository.GetUserClientAssignmentsAsync(
			[authenticatedUserId],
			cancellationToken)).SingleOrDefault();
		if (string.IsNullOrWhiteSpace(assignment?.ClientName))
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
				assignment.ClientName,
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

		await _unitOfWork.BeginTransactionAsync(cancellationToken);

		try
		{
			await _atsRepository.MarkAsDisputedAsync(disputeRequest, cancellationToken);
			await _orderHistoryService.RecordAsync(
				order.EmailInvitationID,
				OrderHistoryEventType.ReportDisputed,
				order.OrderStatus,
				order.OrderStatus ?? OrderStatus.Completed, cancellationToken);

			await _unitOfWork.SaveChangesAsync(cancellationToken);

			await _unitOfWork.CommitAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(cancellationToken);

			_logger.LogError(
				ex,
				"Failed to mark order as disputed. {@Context}",
				logContext);

			throw new InternalServerException("Failed to mark order as disputed.");
		}

		return true;
	}

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
