namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset over OrderCreatedAt descending. EmailInvitationID is only a stable
	// tiebreaker for equal creation timestamps.
	public async Task<List<EmailInvitationRequestListDTO>> GetWithdrawnPageAsync(
		string? searchTerm,
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var usersQuery = BuildWithdrawnQuery(searchTerm, authorizedClientIds, requiredRequestorId);
		if (afterId.HasValue)
		{
			var cursorId = afterId.Value;
			usersQuery = afterCreatedAt.HasValue
				? usersQuery.Where(eir => eir.OrderCreatedAt != null
					&& (eir.OrderCreatedAt < afterCreatedAt.Value
						|| (eir.OrderCreatedAt == afterCreatedAt.Value && eir.EmailInvitationID.CompareTo(cursorId) > 0)))
				: usersQuery.Where(eir =>
					(eir.OrderCreatedAt == null && eir.EmailInvitationID.CompareTo(cursorId) > 0)
					|| eir.OrderCreatedAt != null);
		}

		return await usersQuery
					.OrderByDescending(eir => eir.OrderCreatedAt)
					.ThenBy(eir => eir.EmailInvitationID)
					.Take(take)
					.Select(eir => new EmailInvitationRequestListDTO
					{
						EmailInvitationID = eir.EmailInvitationID,
						EmailAddress = eir.EmailAddress,
						FirstName = eir.FirstName,
						LastName = eir.LastName,
						Requestor = eir.Requestor,
						TicketNumber = eir.TicketNumber,
						OrderCreatedAt = eir.OrderCreatedAt,
						WithdrawnAt = _dbcontext.OrderStatusHistories
							.Where(history => history.EmailInvitationRequestId == eir.EmailInvitationID
								&& history.EventType == OrderHistoryEventType.ApplicationFormWithdrawn)
							.OrderByDescending(history => history.OccurredAt)
							.Select(history => (DateTime?)history.OccurredAt)
							.FirstOrDefault(),
					})
					.ToListAsync(cancellationToken);
	}

	public Task<long> CountWithdrawnAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken) =>
		BuildWithdrawnQuery(searchTerm, authorizedClientIds, requiredRequestorId).LongCountAsync(cancellationToken);

	private IQueryable<EmailInvitationRequest> BuildWithdrawnQuery(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId)
	{
		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => (authorizedClientIds == null
					|| (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
				&& (!requiredRequestorId.HasValue
					|| eir.RequestorId == requiredRequestorId.Value))
			.Where(eir => eir.OrderStatus == OrderStatus.ApplicationWithdrawn);

		if (!string.IsNullOrEmpty(searchTerm))
			usersQuery = usersQuery.Where(eir =>
				EF.Functions.ILike(eir.FirstName!, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.MiddleInitial ?? string.Empty, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.LastName!, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.Requestor ?? string.Empty, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.TicketNumber ?? string.Empty, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.EmailAddress!, $"%{searchTerm}%"));

		return usersQuery;
	}
}
