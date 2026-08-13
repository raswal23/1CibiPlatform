namespace ATS.Data.Repository.OrderHistory;

public class OrderHistoryRepository : IOrderHistoryRepository
{
	private readonly ATSDBContext _dbContext;
	public OrderHistoryRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken)
	{
		await _dbContext.OrderStatusHistories.AddAsync(history, cancellationToken);
		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken) =>
		await _dbContext.OrderStatusHistories.AsNoTracking()
			.Where(x => x.EmailInvitationRequestId == invitationId)
			.OrderBy(x => x.OccurredAt)
			.Select(x => new OrderStatusHistoryDTO(x.OrderStatusHistoryId, x.EventType, x.PreviousStatus, x.NewStatus, x.Source, x.OccurredAt))
			.ToListAsync(cancellationToken);
}
