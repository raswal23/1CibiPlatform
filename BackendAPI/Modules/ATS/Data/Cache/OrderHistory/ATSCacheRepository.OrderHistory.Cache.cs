namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// OrderHistory was never decorated with caching — pure pass-through preserves that.
	public Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken) =>
		_atsRepository.AddAsync(history, cancellationToken);

	public Task AddRangeAsync(IReadOnlyCollection<OrderStatusHistory> histories, CancellationToken cancellationToken) =>
		_atsRepository.AddRangeAsync(histories, cancellationToken);

	public Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken) =>
		_atsRepository.GetAsync(invitationId, cancellationToken);
}
