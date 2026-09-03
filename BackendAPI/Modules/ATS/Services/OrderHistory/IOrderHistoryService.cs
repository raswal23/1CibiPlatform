namespace ATS.Services.OrderHistory;

public interface IOrderHistoryService
{
	Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web);

	/// <summary>
	/// Records the same event for many orders in one insert. Used by the bulk parsing
	/// job, where a single file can create hundreds of orders at once.
	/// </summary>
	Task RecordManyAsync(IReadOnlyCollection<Guid> invitationIds, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web, Guid? changedByUserId = null);
	Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken);
}
