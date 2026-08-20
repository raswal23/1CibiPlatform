namespace ATS.Services.OrderHistory;

public class OrderHistoryService : IOrderHistoryService
{
	private readonly IOrderHistoryFactory _factory;
	private readonly IOrderHistoryRepository _repository;

	public OrderHistoryService(IOrderHistoryFactory factory, IOrderHistoryRepository repository)
	{
		_factory = factory;
		_repository = repository;
	}

	public Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web) =>
		_repository.AddAsync(_factory.Create(invitationId, eventType, previousStatus, newStatus, source), cancellationToken);

	public Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken) =>
		_repository.GetAsync(invitationId, cancellationToken);
}
