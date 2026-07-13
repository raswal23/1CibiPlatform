namespace FrontendWebassembly.Services.ATS.Interface;

public interface IDisputeOrderService
{
	Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null);
    Task<bool> MarkAsDisputedAsync(Guid emailInvitationId);
}
