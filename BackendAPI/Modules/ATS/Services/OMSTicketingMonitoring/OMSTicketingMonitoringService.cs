namespace ATS.Services.OMSTicketingMonitoring;

public sealed class OMSTicketingMonitoringService : IOMSTicketingMonitoringService
{
	// A bulk retry is bounded because each requeued order becomes an OMS round trip on the
	// ticketing job's next passes. Releasing thousands at once would monopolise that job
	// and block every other client behind one operator's click.
	public const int MaxBulkRetrySize = 500;

	private readonly ILogger<OMSTicketingMonitoringService> _logger;
	private readonly IOMSTicketingRepository _ticketingRepository;
	private readonly IAtsAccessScopeResolver _scopeResolver;
	private readonly IOrderHistoryService _orderHistoryService;

	public OMSTicketingMonitoringService(
		ILogger<OMSTicketingMonitoringService> logger,
		IOMSTicketingRepository ticketingRepository,
		IAtsAccessScopeResolver scopeResolver,
		IOrderHistoryService orderHistoryService)
	{
		_logger = logger;
		_ticketingRepository = ticketingRepository;
		_scopeResolver = scopeResolver;
		_orderHistoryService = orderHistoryService;
	}

	public async Task<KeysetPaginatedResult<TicketedOrderListDTO>> GetTicketedOrdersAsync(
		KeysetPaginationRequest paginationRequest,
		string? status,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetTicketedOrders",
			Step = "FetchingTicketedOrders",
			Pagination = paginationRequest,
			Status = status,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching ticketed orders with pagination: {@Context}", logContext);

		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		// A caller outside the role ladder reads an empty list rather than a 403, which
		// is how every other ATS list behaves.
		if (scope is not { } accessScope)
		{
			return new KeysetPaginatedResult<TicketedOrderListDTO>(
				Array.Empty<TicketedOrderListDTO>(),
				null,
				0);
		}

		var normalizedStatus = NormalizeStatus(status);

		// Cursor over the fixed (OrderCreatedAt DESC, EmailInvitationID ASC) ordering.
		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);

		DateTime? afterOrderCreatedAt = DateTime.TryParse(
			fields?[0],
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind,
			out var orderCreatedAt)
			? orderCreatedAt
			: null;

		Guid? afterInvitationId = Guid.TryParse(fields?[1], out var invitationId)
			? invitationId
			: null;

		var hasSeek = afterOrderCreatedAt.HasValue && afterInvitationId.HasValue;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _ticketingRepository.GetTicketedOrdersPageAsync(
			hasSeek ? afterOrderCreatedAt : null,
			hasSeek ? afterInvitationId : null,
			pageSize + 1,
			normalizedStatus,
			paginationRequest.SearchTerm,
			paginationRequest.StartDate,
			paginationRequest.EndDate,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);

		// OrderCreatedAt is nullable on the entity but never null for a queued order,
		// so the cursor falls back to the epoch rather than throwing.
		var nextCursor = hasMore
			? CursorCodec.Encode(
				(page[^1].OrderCreatedAt ?? DateTime.MinValue).ToString("O", CultureInfo.InvariantCulture),
				page[^1].EmailInvitationID.ToString("D"))
			: null;

		long? totalCount = hasSeek
			? null
			: await _ticketingRepository.CountTicketedOrdersAsync(
				normalizedStatus,
				paginationRequest.SearchTerm,
				paginationRequest.StartDate,
				paginationRequest.EndDate,
				accessScope.AuthorizedClientIds,
				accessScope.RequiredOwnerId,
				cancellationToken);

		return new KeysetPaginatedResult<TicketedOrderListDTO>(page, nextCursor, totalCount);
	}

	public async Task<TicketStatusCountsDTO> GetStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken)
	{
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		if (scope is not { } accessScope)
		{
			return new TicketStatusCountsDTO();
		}

		return await _ticketingRepository.GetTicketStatusCountsAsync(
			searchTerm,
			startDate,
			endDate,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);
	}

	public async Task<bool> RetryTicketAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "RetryTicket",
			Step = "RequeueExhaustedOrder",
			EmailInvitationId = emailInvitationId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Retrying OMS ticketing for an order: {@Context}", logContext);

		// Retry takes a caller-supplied id, so the caller's scope is enforced here
		// rather than trusting the page to only offer ids it already listed.
		if (await _scopeResolver.ResolveAsync(cancellationToken) is not { } accessScope)
		{
			throw new ForbiddenException("The current user does not have ATS access.");
		}

		var target = await _ticketingRepository.GetRetryTargetAsync(emailInvitationId, cancellationToken);

		// Out of scope reads as not found: the response must not reveal that an order
		// the caller may not see exists. Same rule the resend path applies.
		if (target is null || !IsWithinScope(target, accessScope))
		{
			_logger.LogWarning("Ticket retry denied for an unknown or out-of-scope order: {@Context}", logContext);

			throw new NotFoundException($"Email invitation with ID {emailInvitationId} not found.");
		}

		var requeued = await _ticketingRepository.RequeueExhaustedTicketAsync(
			emailInvitationId,
			cancellationToken);

		// The button was stale: the job re-claimed the order, someone else retried it,
		// or it is not exhausted at all. Say so rather than reporting a silent success.
		if (!requeued)
		{
			_logger.LogWarning("Ticket retry rejected, the order is no longer retryable: {@Context}", logContext);

			throw new ConflictException(
				"This order is no longer awaiting a retry. Refresh the list to see its current status.");
		}

		// Records who forced the retry. The order's own status is unchanged - this is a
		// ticketing action, not a step in the order lifecycle - so it is written on
		// both sides of the entry.
		await _orderHistoryService.RecordAsync(
			emailInvitationId,
			OrderHistoryEventType.TicketRetryRequested,
			target.OrderStatus,
			target.OrderStatus ?? string.Empty,
			cancellationToken);

		_logger.LogInformation("Order requeued for OMS ticketing: {@Context}", logContext);

		return true;
	}

	public async Task<BulkRetryResultDTO> RetryTicketsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "RetryTickets",
			Step = "RequeueExhaustedOrders",
			RequestedCount = emailInvitationIds.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Retrying OMS ticketing for {Count} order(s): {@Context}", emailInvitationIds.Count, logContext);

		if (await _scopeResolver.ResolveAsync(cancellationToken) is not { } accessScope)
		{
			throw new ForbiddenException("The current user does not have ATS access.");
		}

		// Distinct because a selection can repeat an id, and a duplicate would otherwise be
		// counted twice in the total reported back.
		var requestedIds = emailInvitationIds.Distinct().ToList();

		if (requestedIds.Count > MaxBulkRetrySize)
		{
			throw new BadRequestException(
				$"A bulk retry is limited to {MaxBulkRetrySize} orders at a time. Narrow the selection and try again.");
		}

		var targets = await _ticketingRepository.GetRetryTargetsAsync(requestedIds, cancellationToken);

		// Scope is enforced per row, not once for the request: without this, a caller could
		// touch another client's orders simply by posting their ids alongside their own.
		// Out-of-scope ids are dropped silently rather than reported, for the same reason
		// the single retry answers 404 - naming them would confirm the orders exist.
		var inScopeIds = targets
			.Where(target => IsWithinScope(target, accessScope))
			.Select(target => target.EmailInvitationID)
			.ToList();

		if (inScopeIds.Count == 0)
		{
			throw new NotFoundException("None of the selected orders are available to retry.");
		}

		// Claimed before the update so the set is known: ExecuteUpdate returns a count, not
		// the rows it touched. Anything already requeued or re-claimed by the job simply
		// does not match the predicate.
		var eligibleIds = await _ticketingRepository.GetExhaustedTicketIdsAsync(
			inScopeIds,
			cancellationToken);

		var requeued = await _ticketingRepository.RequeueExhaustedTicketsAsync(
			eligibleIds,
			cancellationToken);

		// Recorded only for orders that were eligible, so a stale id in the selection never
		// produces a history entry claiming a retry that did not happen. The order's own
		// status is unchanged - this is a ticketing action, not a lifecycle step.
		if (eligibleIds.Count > 0)
		{
			await _orderHistoryService.RecordManyAsync(
				eligibleIds,
				OrderHistoryEventType.TicketRetryRequested,
				null,
				string.Empty,
				cancellationToken);
		}

		_logger.LogInformation(
			"Requeued {RequeuedCount} of {RequestedCount} order(s) for OMS ticketing: {@Context}",
			requeued,
			requestedIds.Count,
			logContext);

		return new BulkRetryResultDTO
		{
			RequestedCount = requestedIds.Count,
			RequeuedCount = requeued
		};
	}

	// Mirrors the read path's scope rule: a null client set means unrestricted (super
	// admin), and RequiredOwnerId restricts a user to the orders they raised.
	private static bool IsWithinScope(TicketRetryTargetDTO target, AtsAccessScope scope)
	{
		if (scope.AuthorizedClientIds is { } clientIds
			&& (target.ClientId is not { } clientId || !clientIds.Contains(clientId)))
		{
			return false;
		}

		return !scope.RequiredOwnerId.HasValue
			|| target.RequestorId == scope.RequiredOwnerId.Value;
	}

	// An unrecognised status would otherwise reach the repository as a literal filter
	// and silently return nothing; treat it as "no filter" instead.
	private static string? NormalizeStatus(string? status) =>
		!string.IsNullOrWhiteSpace(status)
			&& TicketStatus.All.FirstOrDefault(known =>
				string.Equals(known, status.Trim(), StringComparison.OrdinalIgnoreCase)) is { } matched
			? matched
			: null;
}
