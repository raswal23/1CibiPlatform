namespace ATS.Data.Repository.Administration.UserClient;

public sealed class UserClientRepository : IUserClientRepository
{
	private readonly ATSDBContext _dbContext;

	public UserClientRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(CancellationToken cancellationToken) =>
		await _dbContext.UserClientDetails.AsNoTracking().OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				ClientName = _dbContext.ClientDetails.Where(client => client.ClientId == assignment.ClientId)
					.Select(client => client.ClientName).FirstOrDefault(),
				CreatedAt = assignment.CreatedAt,
				UpdatedAt = assignment.UpdatedAt
			}).ToListAsync(cancellationToken);

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		int clientId,
		CancellationToken cancellationToken) =>
		await _dbContext.UserClientDetails.AsNoTracking()
			.Where(assignment => assignment.ClientId == clientId)
			.OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				ClientName = _dbContext.ClientDetails
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

		return await _dbContext.UserClientDetails.AsNoTracking()
			.Where(assignment => userIds.Contains(assignment.UserId))
			.OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				ClientName = _dbContext.ClientDetails
					.Where(client => client.ClientId == assignment.ClientId)
					.Select(client => client.ClientName)
					.FirstOrDefault(),
				CreatedAt = assignment.CreatedAt,
				UpdatedAt = assignment.UpdatedAt
			})
			.ToListAsync(cancellationToken);
	}

	public async Task<PaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(
		PaginationRequest request,
		CancellationToken cancellationToken)
	{
		var query = _dbContext.ClientDetails.AsNoTracking().Where(client => client.IsActive);
		if (!string.IsNullOrWhiteSpace(request.SearchTerm))
		{
			var term = $"%{request.SearchTerm.Trim()}%";
			query = query.Where(client => EF.Functions.ILike(client.ClientName, term));
		}

		var logicalClients = query
			.GroupBy(client => client.ClientId)
			.Select(group => new ClientLookupDTO
			{
				ClientId = group.Key,
				ClientName = group.Min(client => client.ClientName)!
			});
		var count = await logicalClients.LongCountAsync(cancellationToken);
		var clients = await logicalClients
			.OrderBy(client => client.ClientName)
			.ThenBy(client => client.ClientId)
			.Skip((request.PageIndex - 1) * request.PageSize)
			.Take(request.PageSize)
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ClientLookupDTO>(
			request.PageIndex,
			request.PageSize,
			count,
			clients);
	}

	public Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken) =>
		_dbContext.UserClientDetails.AsNoTracking().FirstOrDefaultAsync(assignment => assignment.UserId == userId, cancellationToken);

	public Task<bool> ClientIsActiveAsync(int clientId, CancellationToken cancellationToken) =>
		_dbContext.ClientDetails.AsNoTracking()
			.AnyAsync(client => client.ClientId == clientId && client.IsActive, cancellationToken);

	public async Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var userClient = await _dbContext.UserClientDetails.FirstOrDefaultAsync(item => item.UserId == assignment.UserId, cancellationToken);
		if (userClient?.ClientId == assignment.ClientId)
			return userClient;

		if (userClient is null)
		{
			userClient = new UserClientDetails { UserId = assignment.UserId, ClientId = assignment.ClientId, CreatedAt = now, UpdatedAt = now };
			await _dbContext.UserClientDetails.AddAsync(userClient, cancellationToken);
		}
		else
		{
			userClient.ClientId = assignment.ClientId;
			userClient.UpdatedAt = now;
		}

		var accessRows = await _dbContext.UserDetails.Where(user => user.UserId == assignment.UserId).ToListAsync(cancellationToken);
		foreach (var row in accessRows)
		{
			row.ClientId = assignment.ClientId;
			row.UpdatedAt = now;
		}
		await _dbContext.SaveChangesAsync(cancellationToken);
		return userClient;
	}
}
