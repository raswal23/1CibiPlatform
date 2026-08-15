namespace ATS.Data.Repository.Administration.Clients;

public sealed class ClientRepository : IClientRepository
{
	private readonly ATSDBContext _dbContext;

	public ClientRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(PaginationRequest request, CancellationToken cancellationToken) =>
		GetPageAsync(request, false, cancellationToken);

	public Task<PaginatedResult<ClientDetailsDTO>> SearchClientsAsync(PaginationRequest request, CancellationToken cancellationToken) =>
		GetPageAsync(request, true, cancellationToken);

	public async Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var clients = clientDTOs.ToArray();
		var clientName = clients[0].ClientName.Trim();

		var now = DateTime.UtcNow;
		await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
		var first = new ClientDetails
		{
			ClientName = clientName,
			ClientDescription = clients[0].ClientDescription.Trim(),
			IsActive = clients[0].IsActive,
			PackageId = clients[0].PackageId,
			CreatedAt = now,
			UpdatedAt = now
		};
		await _dbContext.ClientDetails.AddAsync(first, cancellationToken);
		await _dbContext.SaveChangesAsync(cancellationToken);
		await _dbContext.ClientDetails.AddRangeAsync(clients.Skip(1).Select(client => new ClientDetails
		{
			ClientId = first.ClientId,
			ClientName = clientName,
			ClientDescription = clients[0].ClientDescription.Trim(),
			IsActive = clients[0].IsActive,
			PackageId = client.PackageId,
			CreatedAt = now,
			UpdatedAt = now
		}), cancellationToken);
		await _dbContext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken) =>
		await _dbContext.ClientDetails.AsNoTracking().Where(client => client.ClientId == clientId)
			.OrderBy(client => client.PackageId).ToListAsync(cancellationToken);

	public Task<bool> ClientNameExistsAsync(string clientName, int? excludeClientId, CancellationToken cancellationToken) =>
		_dbContext.ClientDetails.AsNoTracking()
			.AnyAsync(client => (!excludeClientId.HasValue || client.ClientId != excludeClientId.Value)
				&& EF.Functions.ILike(client.ClientName, clientName), cancellationToken);

	public Task<int> CountActivePackagesAsync(IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken) =>
		_dbContext.PackageDetails.AsNoTracking()
			.CountAsync(package => packageIds.Contains(package.PackageId) && package.IsActive, cancellationToken);

	public async Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var clients = clientDTOs.ToArray();
		var clientId = clients[0].ClientId;
		var existing = await _dbContext.ClientDetails.Where(client => client.ClientId == clientId).ToListAsync(cancellationToken);

		var name = clients[0].ClientName.Trim();
		var selectedPackageIds = clients.Select(client => client.PackageId).Distinct().ToHashSet();
		var existingPackageIds = existing.Select(client => client.PackageId).ToHashSet();
		var newPackageIds = selectedPackageIds.Except(existingPackageIds).ToArray();

		var now = DateTime.UtcNow;
		var createdAt = existing.Min(client => client.CreatedAt);
		var description = clients[0].ClientDescription.Trim();
		var isActive = clients[0].IsActive;
		_dbContext.ClientDetails.RemoveRange(existing.Where(client => !selectedPackageIds.Contains(client.PackageId)));
		foreach (var client in existing.Where(client => selectedPackageIds.Contains(client.PackageId)))
		{
			client.ClientName = name;
			client.ClientDescription = description;
			client.IsActive = isActive;
			client.UpdatedAt = now;
		}
		var added = newPackageIds.Select(packageId => new ClientDetails
		{
			ClientId = clientId,
			ClientName = name,
			ClientDescription = description,
			IsActive = isActive,
			PackageId = packageId,
			CreatedAt = createdAt,
			UpdatedAt = now
		}).ToArray();
		await _dbContext.ClientDetails.AddRangeAsync(added, cancellationToken);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return existing.Where(client => selectedPackageIds.Contains(client.PackageId)).Concat(added).OrderBy(client => client.PackageId).ToArray();
	}

	private async Task<PaginatedResult<ClientDetailsDTO>> GetPageAsync(PaginationRequest request, bool search, CancellationToken cancellationToken)
	{
		var query = _dbContext.ClientDetails.AsNoTracking();
		if (search)
		{
			var term = $"%{request.SearchTerm}%";
			query = query.Where(client => EF.Functions.ILike(client.ClientName, term) || EF.Functions.ILike(client.ClientDescription, term));
		}
		var logical = query.GroupBy(client => client.ClientId).Select(group => new { ClientId = group.Key, ClientName = group.Min(client => client.ClientName) });
		var count = await logical.LongCountAsync(cancellationToken);
		var ids = await logical.OrderBy(client => client.ClientName).ThenBy(client => client.ClientId)
			.Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize).Select(client => client.ClientId).ToListAsync(cancellationToken);
		var items = await query.Where(client => ids.Contains(client.ClientId)).OrderBy(client => client.ClientName)
			.ThenBy(client => client.ClientId).ThenBy(client => client.PackageId)
			.Select(client => new ClientDetailsDTO
			{
				ClientId = client.ClientId,
				ClientName = client.ClientName,
				ClientDescription = client.ClientDescription,
				IsActive = client.IsActive,
				PackageId = client.PackageId,
				CreatedAt = client.CreatedAt,
				UpdatedAt = client.UpdatedAt
			}).ToListAsync(cancellationToken);
		return new PaginatedResult<ClientDetailsDTO>(request.PageIndex, request.PageSize, count, items);
	}
}
