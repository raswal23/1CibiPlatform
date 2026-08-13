namespace ATS.Services.OrderHistory;

public interface IOrderHistoryFactory
{
	OrderStatusHistory Create(Guid invitationId, string eventType, string? previousStatus, string newStatus, string source = OrderHistorySource.Web);
}
