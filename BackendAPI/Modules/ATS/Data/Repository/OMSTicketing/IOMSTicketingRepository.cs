namespace ATS.Data.Repository.OMSTicketing;

public interface IOMSTicketingRepository
{
	/// <summary>
	/// Atomically moves a batch of un-ticketed orders to Processing and returns them,
	/// so a concurrent worker cannot ticket the same order twice.
	/// </summary>
	Task<List<EmailInvitationRequest>> ClaimPendingTicketsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Returns anything claimed longer ago than <paramref name="staleAfter"/> to Pending.
	/// A crash mid-call leaves rows Processing with no live worker.
	/// </summary>
	Task<int> ReleaseStaleTicketClaimsAsync(TimeSpan staleAfter, CancellationToken cancellationToken);

	/// <summary>
	/// Loads everything the OMS payload needs for the claimed ids in one round trip.
	/// </summary>
	Task<List<TicketablePayloadDTO>> GetTicketPayloadsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken);

	/// <summary>
	/// Terminal success: records the OMS ticket and retires the row from the queue.
	/// </summary>
	Task<bool> MarkTicketedAsync(
		Guid emailInvitationId,
		string ticketNumber,
		DateTime deliveryDate,
		CancellationToken cancellationToken);

	/// <summary>
	/// Releases a claim as Error and counts the attempt. <paramref name="isRetryable"/>
	/// false burns the whole retry budget at once, so a request that cannot succeed
	/// without human intervention is not re-attempted every tick.
	/// </summary>
	Task<int> MarkTicketFailedAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		string reason,
		bool isRetryable,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the scope identity and current order status of one order, for the access
	/// check and history entry a manual retry needs. Returns null when the order does
	/// not exist.
	/// </summary>
	Task<TicketRetryTargetDTO?> GetRetryTargetAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Puts an order whose automatic retries are exhausted back on the queue with a
	/// fresh attempt budget. Returns false when the order is not in that state - it was
	/// already requeued, already ticketed, or is still auto-retrying - so a stale button
	/// cannot resurrect a live claim.
	/// </summary>
	Task<bool> RequeueExhaustedTicketAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken);

	Task<List<TicketedOrderListDTO>> GetTicketedOrdersPageAsync(
		DateTime? afterOrderCreatedAt,
		Guid? afterInvitationId,
		int take,
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);

	Task<long> CountTicketedOrdersAsync(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);

	Task<TicketStatusCountsDTO> GetTicketStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
}
