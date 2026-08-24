namespace ATS.Data.Repository;

public partial class ATSRepository
{
	public async Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken)
	{
		await _dbcontext.OrderStatusHistories.AddAsync(history, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken) =>
		await _dbcontext.OrderStatusHistories.AsNoTracking()
			.Where(x => x.EmailInvitationRequestId == invitationId)
			.OrderBy(x => x.OccurredAt)
			.Select(x => new OrderStatusHistoryDTO(x.OrderStatusHistoryId, x.EventType, x.PreviousStatus, x.NewStatus, x.Source, x.OccurredAt))
			.ToListAsync(cancellationToken);
}
