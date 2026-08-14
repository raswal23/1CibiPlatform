namespace ATS.Data.Repository.OrderHistory;

public interface IOrderHistoryRepository
{
	Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken);
	Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken);
}
