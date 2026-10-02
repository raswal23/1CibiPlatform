namespace ATS.Services.DisputeOrder;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly ILogger<DisputeOrderService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IUserClientRepository _userClientRepository;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly ICurrentUser _currentUser;
	private readonly IAtsAccessScopeResolver _accessScopeResolver;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IDisputeEmailNotification _disputeEmailNotification;

	public DisputeOrderService(
		ILogger<DisputeOrderService> logger,
		IATSRepository atsRepository,
		IUserClientRepository userClientRepository,
		IHttpContextAccessor httpContextAccessor,
		IOrderHistoryService orderHistoryService,
		ICurrentUser currentUser,
		IAtsAccessScopeResolver accessScopeResolver,
		IUnitOfWork unitOfWork,
		IDisputeEmailNotification disputeEmailNotification)
	{
		_accessScopeResolver = accessScopeResolver;
		_logger = logger;
		_atsRepository = atsRepository;
		_userClientRepository = userClientRepository;
		_httpContextAccessor = httpContextAccessor;
		_orderHistoryService = orderHistoryService;
		_currentUser = currentUser;
		_unitOfWork = unitOfWork;
		_disputeEmailNotification = disputeEmailNotification;
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

		// Cursor over the fixed (completedAt, id) ordering. Both anchors are required;
		// a malformed cursor restarts the walk from the first page.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		DateTime? afterCompletedAt = DateTime.TryParse(fields?[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var completedAt) ? completedAt : null;
		Guid? afterId = Guid.TryParse(fields?[1], out var invitationId) ? invitationId : null;
		var hasSeek = afterCompletedAt.HasValue && afterId.HasValue;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _atsRepository.GetDisputeOrdersPageAsync(
			paginationRequest.SearchTerm, hasSeek ? afterCompletedAt : null, hasSeek ? afterId : null,
			pageSize + 1, clientIds, requiredRequestorId, cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		string? nextCursor = null;
		if (hasMore)
		{
			var last = items[^1];
			nextCursor = CursorCodec.Encode(
				last.OrderCompletedAt!.Value.ToString("O"),
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

			// After the commit, and it cannot throw - DisputeEmailNotification guards itself, the
			// same contract WithdrawnEmailNotification keeps. The dispute is durable by now, so a
			// dead mailbox must not turn a filed dispute into a 500 that invites the filer to
			// submit the same dispute a second time.
			//
			// This is the only email a dispute produces. The internal operations alert that used to
			// run BEFORE the write - and throw, so an SMTP outage blocked dispute filing entirely -
			// has been removed. CIBI now sees disputes through the copied address on this message,
			// which is what makes the acknowledgement being best-effort acceptable: losing it costs
			// a courtesy receipt, not the only evidence that a dispute was filed. The filing itself
			// is already committed and already on the order's history.
			//
			// The filer's own address is reused rather than re-read, so the message names whoever
			// actually pressed the button even on a token that only carries the short "email" claim.
			var candidateName = string.IsNullOrWhiteSpace(subjectName)
				? order.EmailAddress ?? "this order"
				: subjectName;

			await _disputeEmailNotification.SendAsync(
				new DisputeEmailDetails(
					order.EmailInvitationID,
					requestor,
					_currentUser.FullName,
					candidateName,
					disputeRequest.DisputeCategory,
					disputeRequest.DisputeReason!),
				cancellationToken);
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
}
