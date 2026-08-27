namespace ATS.Features.OMSTicketing.Query.GetTicketedOrders;

public record GetTicketedOrdersQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Status = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetTicketedOrdersQueryResult>;

public record GetTicketedOrdersQueryResult(KeysetPaginatedResult<TicketedOrderListDTO> TicketedOrders);

public class GetTicketedOrdersQueryRequestValidator : AbstractValidator<GetTicketedOrdersQueryRequest>
{
	public GetTicketedOrdersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		// Cursor is deliberately unvalidated: cursors are opaque and a malformed one
		// self-heals to the first page rather than failing the request.
		RuleFor(x => x.Status)
			.Must(status => string.IsNullOrWhiteSpace(status)
				|| TicketStatus.All.Contains(status, StringComparer.OrdinalIgnoreCase))
			.WithMessage($"Status must be empty or one of: {string.Join(", ", TicketStatus.All)}.");
	}
}

public class GetTicketedOrdersHandler
	: IQueryHandler<GetTicketedOrdersQueryRequest, GetTicketedOrdersQueryResult>
{
	private readonly IOMSTicketingMonitoringService _ticketingMonitoringService;

	public GetTicketedOrdersHandler(IOMSTicketingMonitoringService ticketingMonitoringService)
	{
		_ticketingMonitoringService = ticketingMonitoringService;
	}

	public async Task<GetTicketedOrdersQueryResult> Handle(
		GetTicketedOrdersQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		var ticketedOrders = await _ticketingMonitoringService.GetTicketedOrdersAsync(
			paginationRequest,
			request.Status,
			cancellationToken);

		return new GetTicketedOrdersQueryResult(ticketedOrders);
	}
}
