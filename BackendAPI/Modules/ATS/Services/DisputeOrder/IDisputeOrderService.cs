namespace ATS.Services.DisputeOrder;

public interface IDisputeOrderService
{
	Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, Guid authenticatedUserId, CancellationToken cancellationToken);
}
