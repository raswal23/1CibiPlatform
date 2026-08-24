namespace FrontendWebassembly.Services.ATS.DisputeOrder;

public interface IDisputeOrderService
{
	Task<ServiceResponse<KeysetPaginatedResult<DisputeOrderListDTO>>> GetDisputeOrdersAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null);
    Task<ServiceResponse<bool>> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest);
}
