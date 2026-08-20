namespace ATS.Services.OrderHistory;

public class OrderHistoryService : IOrderHistoryService
{
	private readonly IOrderHistoryFactory _factory;
	private readonly IOrderHistoryRepository _repository;
	private readonly IATSRepository _atsRepository;
	private readonly AtsQueryScopeResolver _scopeResolver;

	public OrderHistoryService(IOrderHistoryFactory factory, IOrderHistoryRepository repository, IATSRepository atsRepository, AtsQueryScopeResolver scopeResolver)
	{
		_factory = factory;
		_repository = repository;
		_atsRepository = atsRepository;
		_scopeResolver = scopeResolver;
	}

	public Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web) =>
		_repository.AddAsync(_factory.Create(invitationId, eventType, previousStatus, newStatus, source), cancellationToken);

	public async Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken)
	{
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);
		var order = await _atsRepository.GetEmailInvitationRequestByIdAsync(invitationId, cancellationToken);
		if (order.EmailInvitationID == Guid.Empty) throw new NotFoundException("Order not found.");
		var allowed = scope.Kind switch
		{
			AtsQueryScopeKind.All => true,
			AtsQueryScopeKind.Client => order.ClientId == scope.ClientId,
			AtsQueryScopeKind.Clients => order.ClientId.HasValue && scope.ClientIds.Contains(order.ClientId.Value),
			AtsQueryScopeKind.ClientRequestor => order.ClientId == scope.ClientId && order.RequestorId == scope.RequestorId,
			AtsQueryScopeKind.Requestor => order.RequestorId == scope.RequestorId,
			_ => false
		};
		if (!allowed) throw new ForbiddenException("The current user does not have access to this order history.");
		return await _repository.GetAsync(invitationId, cancellationToken);
	}
}
