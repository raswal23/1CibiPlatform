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

	/// <summary>
	/// Puts an order whose automatic OMS retries are exhausted back on the ticketing
	/// queue. Throws NotFoundException when the order is unknown or outside the
	/// caller's scope, and ConflictException when it is no longer retryable.
	/// </summary>
	Task<bool> RetryTicketAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken);
}
