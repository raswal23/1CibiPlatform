namespace ATS.Services.DisputeOrder;

public interface IDisputeOrderService
{
	Task<KeysetPaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, Guid authenticatedUserId, CancellationToken cancellationToken);
}
