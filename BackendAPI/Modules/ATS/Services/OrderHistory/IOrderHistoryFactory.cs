namespace ATS.Services.OrderHistory;

public interface IOrderHistoryFactory
{
	/// <summary>
	/// Builds one history entry. <paramref name="changedByUserId"/> overrides the
	/// ambient caller, for background work that has no HTTP context and must name the
	/// user who originally requested the change.
	/// </summary>
	OrderStatusHistory Create(Guid invitationId, string eventType, string? previousStatus, string newStatus, string source = OrderHistorySource.Web, Guid? changedByUserId = null);
}
