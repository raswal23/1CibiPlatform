namespace ATS.Services.OMSTicketingMonitoring;

public interface IOMSTicketingMonitoringService
{
	Task<KeysetPaginatedResult<TicketedOrderListDTO>> GetTicketedOrdersAsync(
		KeysetPaginationRequest paginationRequest,
		string? status,
		CancellationToken cancellationToken);

	Task<TicketStatusCountsDTO> GetStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken);
}
