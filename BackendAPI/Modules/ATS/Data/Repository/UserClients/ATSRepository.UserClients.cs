namespace ATS.Data.Repository;

public partial class ATSRepository
{
	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(CancellationToken cancellationToken) =>
		await _dbcontext.UserClientDetails.AsNoTracking().OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				ClientName = _dbcontext.ClientDetails.Where(client => client.ClientId == assignment.ClientId)
					.Select(client => client.ClientName).FirstOrDefault(),
				CreatedAt = assignment.CreatedAt,
				UpdatedAt = assignment.UpdatedAt
			}).ToListAsync(cancellationToken);

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		int clientId,
		CancellationToken cancellationToken) =>
		await _dbcontext.UserClientDetails.AsNoTracking()
			.Where(assignment => assignment.ClientId == clientId)
			.OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				ClientName = _dbcontext.ClientDetails
					.Where(client => client.ClientId == assignment.ClientId)
					.Select(client => client.ClientName)
					.FirstOrDefault(),
				CreatedAt = assignment.CreatedAt,
				UpdatedAt = assignment.UpdatedAt
			})
			.ToListAsync(cancellationToken);

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		IReadOnlyCollection<Guid> userIds,
		CancellationToken cancellationToken)
	{
		if (userIds.Count == 0)
			return Array.Empty<UserClientDetailsDTO>();

		return await _dbcontext.UserClientDetails.AsNoTracking()
			.Where(assignment => userIds.Contains(assignment.UserId))
			.OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				ClientName = _dbcontext.ClientDetails
					.Where(client => client.ClientId == assignment.ClientId)
					.Select(client => client.ClientName)
					.FirstOrDefault(),
				CreatedAt = assignment.CreatedAt,
				UpdatedAt = assignment.UpdatedAt
			})
			.ToListAsync(cancellationToken);
	}

	// Keyset over the grouped projection (ClientName, ClientId); the predicate on
	// the Min() aggregate translates to HAVING/subquery filtering on Postgres.
	// Pure query — the service decodes the cursor and mints the next one.
	public async Task<List<ClientLookupDTO>> GetAssignableClientsPageAsync(
		string? searchTerm, string? afterClientName, int? afterClientId, int take,
		CancellationToken cancellationToken)
	{
		var logicalClients = BuildAssignableClientsQuery(searchTerm);
		if (afterClientName is not null && afterClientId.HasValue)
		{
			var cId = afterClientId.Value;
			logicalClients = logicalClients.Where(client =>
				string.Compare(client.ClientName, afterClientName) > 0
				|| (client.ClientName == afterClientName && client.ClientId > cId));
		}

		return await logicalClients
			.OrderBy(client => client.ClientName)
			.ThenBy(client => client.ClientId)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountAssignableClientsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildAssignableClientsQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<ClientLookupDTO> BuildAssignableClientsQuery(string? searchTerm)
	{
		var query = _dbcontext.ClientDetails.AsNoTracking().Where(client => client.IsActive);
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var term = $"%{searchTerm.Trim()}%";
			query = query.Where(client => EF.Functions.ILike(client.ClientName, term));
		}

		return query
			.GroupBy(client => client.ClientId)
			.Select(group => new ClientLookupDTO
			{
				ClientId = group.Key,
				ClientName = group.Min(client => client.ClientName)!
			});
	}

	public Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken) =>
		_dbcontext.UserClientDetails.AsNoTracking().FirstOrDefaultAsync(assignment => assignment.UserId == userId, cancellationToken);

	public Task<bool> ClientIsActiveAsync(int clientId, CancellationToken cancellationToken) =>
		_dbcontext.ClientDetails.AsNoTracking()
			.AnyAsync(client => client.ClientId == clientId && client.IsActive, cancellationToken);

	public async Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var userClient = await _dbcontext.UserClientDetails.FirstOrDefaultAsync(item => item.UserId == assignment.UserId, cancellationToken);
		if (userClient?.ClientId == assignment.ClientId)
			return userClient;

		if (userClient is null)
		{
			userClient = new UserClientDetails { UserId = assignment.UserId, ClientId = assignment.ClientId, CreatedAt = now, UpdatedAt = now };
			await _dbcontext.UserClientDetails.AddAsync(userClient, cancellationToken);
		}
		else
		{
			userClient.ClientId = assignment.ClientId;
			userClient.UpdatedAt = now;
		}

		var accessRows = await _dbcontext.UserDetails.Where(user => user.UserId == assignment.UserId).ToListAsync(cancellationToken);
		foreach (var row in accessRows)
		{
			row.ClientId = assignment.ClientId;
			row.UpdatedAt = now;
		}
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return userClient;
	}
}
