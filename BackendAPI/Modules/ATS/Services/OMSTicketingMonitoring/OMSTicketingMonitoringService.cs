namespace ATS.Services.OMSTicketingMonitoring;

public sealed class OMSTicketingMonitoringService : IOMSTicketingMonitoringService
{
	private readonly ILogger<OMSTicketingMonitoringService> _logger;
	private readonly IOMSTicketingRepository _ticketingRepository;
	private readonly IAtsAccessScopeResolver _scopeResolver;

	public OMSTicketingMonitoringService(
		ILogger<OMSTicketingMonitoringService> logger,
		IOMSTicketingRepository ticketingRepository,
		IAtsAccessScopeResolver scopeResolver)
	{
		_logger = logger;
		_ticketingRepository = ticketingRepository;
		_scopeResolver = scopeResolver;
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

	// An unrecognised status would otherwise reach the repository as a literal filter
	// and silently return nothing; treat it as "no filter" instead.
	private static string? NormalizeStatus(string? status) =>
		!string.IsNullOrWhiteSpace(status)
			&& TicketStatus.All.FirstOrDefault(known =>
				string.Equals(known, status.Trim(), StringComparison.OrdinalIgnoreCase)) is { } matched
			? matched
			: null;
}
