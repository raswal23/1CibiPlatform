namespace ATS.Features.Web.OMSTicketing.Query.GetTicketStatusCounts;

public record GetTicketStatusCountsQueryRequest(
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetTicketStatusCountsQueryResult>;

public record GetTicketStatusCountsQueryResult(TicketStatusCountsDTO Counts);

public class GetTicketStatusCountsHandler
	: IQueryHandler<GetTicketStatusCountsQueryRequest, GetTicketStatusCountsQueryResult>
{
	private readonly IOMSTicketingMonitoringService _ticketingMonitoringService;

	public GetTicketStatusCountsHandler(IOMSTicketingMonitoringService ticketingMonitoringService)
	{
		_ticketingMonitoringService = ticketingMonitoringService;
	}

	public async Task<GetTicketStatusCountsQueryResult> Handle(
		GetTicketStatusCountsQueryRequest request,
		CancellationToken cancellationToken)
	{
		var counts = await _ticketingMonitoringService.GetStatusCountsAsync(
			request.SearchTerm,
			request.StartDate,
			request.EndDate,
			cancellationToken);

		return new GetTicketStatusCountsQueryResult(counts);
	}
}
