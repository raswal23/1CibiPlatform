namespace FrontendWebassembly.Services.ATS.OMSTicketing;

public interface IOMSTicketingService
{
	Task<ServiceResponse<KeysetPaginatedResult<TicketedOrderListDTO>>> GetTicketedOrdersAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? status = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null);

	Task<ServiceResponse<TicketStatusCountsDTO>> GetStatusCountsAsync(
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null);
}
