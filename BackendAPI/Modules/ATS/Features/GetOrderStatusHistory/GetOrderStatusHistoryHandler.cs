namespace ATS.Features.GetOrderStatusHistory;

public record GetOrderStatusHistoryQuery(Guid EmailInvitationRequestId) : IQuery<GetOrderStatusHistoryResult>;
public record GetOrderStatusHistoryResult(IReadOnlyList<OrderStatusHistoryDTO> History);

public class GetOrderStatusHistoryHandler(IOrderHistoryService service) : IQueryHandler<GetOrderStatusHistoryQuery, GetOrderStatusHistoryResult>
{
	public async Task<GetOrderStatusHistoryResult> Handle(GetOrderStatusHistoryQuery request, CancellationToken cancellationToken) =>
		new(await service.GetAsync(request.EmailInvitationRequestId, cancellationToken));
}
