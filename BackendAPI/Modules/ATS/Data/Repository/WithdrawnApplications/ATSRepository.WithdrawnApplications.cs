namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset page over withdrawn invitations ordered by EmailInvitationID (unique PK).
	// Pure query — the service decodes the cursor and mints the next one.
	public async Task<List<EmailInvitationRequestListDTO>> GetWithdrawnPageAsync(
		string? searchTerm,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var usersQuery = BuildWithdrawnQuery(searchTerm, authorizedClientIds, requiredRequestorId);
		if (afterId.HasValue)
			usersQuery = usersQuery.Where(eir => eir.EmailInvitationID.CompareTo(afterId.Value) > 0);

		return await usersQuery
					.OrderBy(eir => eir.EmailInvitationID)
					.Take(take)
					.Select(eir => new EmailInvitationRequestListDTO
					{
						EmailInvitationID = eir.EmailInvitationID,
						EmailAddress = eir.EmailAddress,
						FirstName = eir.FirstName,
						LastName = eir.LastName,
						Requestor = eir.Requestor,
						OrderStatus = eir.OrderStatus,
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
				EF.Functions.ILike(eir.EmailAddress!, $"%{searchTerm}%"));

		return usersQuery;
	}
}
