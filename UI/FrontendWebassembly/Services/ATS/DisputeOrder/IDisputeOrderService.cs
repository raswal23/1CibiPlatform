namespace FrontendWebassembly.Services.ATS.DisputeOrder;

public interface IDisputeOrderService
{
	Task<ServiceResponse<PaginatedResult<DisputeOrderListDTO>>> GetDisputeOrdersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null);
    Task<ServiceResponse<bool>> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest);
}
