namespace ATS.Services.OrderHistory;

public class OrderHistoryFactory : IOrderHistoryFactory
{
	private readonly ICurrentUser _currentUser;

	public OrderHistoryFactory(ICurrentUser currentUser) => _currentUser = currentUser;

	public OrderStatusHistory Create(Guid invitationId, string eventType, string? previousStatus, string newStatus, string source = OrderHistorySource.Web) => new()
	{
		OrderStatusHistoryId = Guid.CreateVersion7(),
		EmailInvitationRequestId = invitationId,
		EventType = eventType,
		PreviousStatus = previousStatus,
		NewStatus = newStatus,
		Source = source,
		OccurredAt = DateTime.UtcNow,
		ChangedByUserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId
	};
}
