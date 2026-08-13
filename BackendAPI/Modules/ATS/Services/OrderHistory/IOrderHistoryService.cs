namespace ATS.Services.OrderHistory;

public interface IOrderHistoryService
{
	Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web);
	Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken);
}
