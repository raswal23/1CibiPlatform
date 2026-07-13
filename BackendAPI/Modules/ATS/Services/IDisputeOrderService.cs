namespace ATS.Services;

public interface IDisputeOrderService
{
	Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
    Task<bool> MarkAsDisputedAsync(Guid emailInvitationId, CancellationToken cancellationToken);
}
