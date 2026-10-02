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

	/// <summary>
	/// The set form of <see cref="RetryTicketAsync"/>, for the ticketing board's
	/// multi-select. Ids the caller may not see are dropped silently - naming them would
	/// confirm those orders exist - and ids that are no longer exhausted are skipped, so
	/// the result reports requested versus actually requeued rather than throwing on a
	/// partly-stale selection.
	///
	/// Throws BadRequestException above <see cref="OMSTicketingMonitoringService.MaxBulkRetrySize"/>,
	/// and NotFoundException when nothing in the selection is available to the caller.
	/// </summary>
	Task<BulkRetryResultDTO> RetryTicketsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken);
}
