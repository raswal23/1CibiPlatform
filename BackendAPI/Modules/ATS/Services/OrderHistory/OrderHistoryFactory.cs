namespace ATS.Services.OrderHistory;

public class OrderHistoryFactory : IOrderHistoryFactory
{
	private readonly ICurrentUser _currentUser;

	public OrderHistoryFactory(ICurrentUser currentUser) => _currentUser = currentUser;

	public OrderStatusHistory Create(Guid invitationId, string eventType, string? previousStatus, string newStatus, string source = OrderHistorySource.Web, Guid? changedByUserId = null)
	{
		// Background jobs run with no HttpContext, so ICurrentUser resolves to null
		// there. They pass the originating user explicitly instead.
		var userId = changedByUserId ?? _currentUser.UserId;

		return new OrderStatusHistory
		{
			OrderStatusHistoryId = Guid.CreateVersion7(),
			EmailInvitationRequestId = invitationId,
			EventType = eventType,
			PreviousStatus = previousStatus,
			NewStatus = newStatus,
			Source = source,
			OccurredAt = DateTime.UtcNow,
			ChangedByUserId = userId == Guid.Empty ? null : userId
		};
	}
}
