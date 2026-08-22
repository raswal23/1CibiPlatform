namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset over (hasDispute DESC, OrderCreatedAt DESC, EmailInvitationID ASC). The
	// computed lead key must appear verbatim in the seek predicate so it matches the
	// ORDER BY expression. OrderCreatedAt is never NULL here — the base filter
	// requires OrderCreatedAt.HasValue. The 30-day window and dispute flags move
	// between requests; rows shifting out of (or ahead of) the cursor simply drop
	// from the walk — keyset never duplicates rows.
	// Pure query — the service decodes the cursor and mints the next one.
	public async Task<List<DisputeOrderListDTO>> GetDisputeOrdersPageAsync(
		string? searchTerm,
		bool? afterHasDispute,
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildDisputeOrdersQuery(searchTerm, authorizedClientIds, requiredRequestorId);
		if (afterHasDispute.HasValue && afterCreatedAt.HasValue && afterId.HasValue)
		{
			var cCreatedAt = afterCreatedAt.Value;
			var cId = afterId.Value;
			pageQuery = afterHasDispute.Value
				? pageQuery.Where(eir =>
					string.IsNullOrEmpty(eir.DisputeCategory)
					|| (!string.IsNullOrEmpty(eir.DisputeCategory)
						&& (eir.OrderCreatedAt < cCreatedAt
							|| (eir.OrderCreatedAt == cCreatedAt && eir.EmailInvitationID.CompareTo(cId) > 0))))
				: pageQuery.Where(eir =>
					string.IsNullOrEmpty(eir.DisputeCategory)
					&& (eir.OrderCreatedAt < cCreatedAt
						|| (eir.OrderCreatedAt == cCreatedAt && eir.EmailInvitationID.CompareTo(cId) > 0)));
		}

		return await pageQuery
			.OrderByDescending(eir => !string.IsNullOrEmpty(eir.DisputeCategory))
			.ThenByDescending(eir => eir.OrderCreatedAt)
			.ThenBy(eir => eir.EmailInvitationID)
			.Take(take)
			.Select(eir => new DisputeOrderListDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				LastName = eir.LastName,
				Requestor = eir.Requestor,
				DisputeCategory = eir.DisputeCategory,
				OrderCreatedAt = eir.OrderCreatedAt,
				OrderCompletedAt = eir.OrderCompletedAt,
			})
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountDisputeOrdersAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken) =>
		BuildDisputeOrdersQuery(searchTerm, authorizedClientIds, requiredRequestorId).LongCountAsync(cancellationToken);

	private IQueryable<EmailInvitationRequest> BuildDisputeOrdersQuery(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId)
	{
		var disputeWindowStart = DateTime.UtcNow.AddDays(-30);

		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => (authorizedClientIds == null
					|| (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
				&& (!requiredRequestorId.HasValue
					|| eir.RequestorId == requiredRequestorId.Value))
			.Where(eir => eir.OrderStatus == OrderStatus.Completed && eir.OrderCreatedAt.HasValue && eir.OrderCompletedAt!.Value >= disputeWindowStart);

		if (!string.IsNullOrEmpty(searchTerm))
			usersQuery = usersQuery.Where(eir =>
				EF.Functions.ILike(eir.FirstName!, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.LastName!, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.Requestor ?? string.Empty, $"%{searchTerm}%") ||
				EF.Functions.ILike(eir.EmailAddress!, $"%{searchTerm}%"));

		return usersQuery;
	}

	public async Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken)
	{
		var affectedRows = await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == disputeRequest.EmailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.DisputeCategory, disputeRequest.DisputeReason)
				.SetProperty(eir => eir.DisputedAt, DateTime.UtcNow),
				cancellationToken);

		return affectedRows > 0;
	}
}
