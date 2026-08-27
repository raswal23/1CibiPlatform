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

	// Returns a 404 detail when the order is unknown or outside the caller's scope, and
	// a 409 detail when it is no longer awaiting a retry; both reach the snackbar.
	Task<ServiceResponse<bool>> RetryTicketAsync(Guid emailInvitationId);
}
