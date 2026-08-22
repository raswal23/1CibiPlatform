namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset over the grouped projection (ClientName, ClientId): one page is take-1
	// logical clients, each expanding to a row per package via GetClientsByIdsAsync.
	// The service mints the cursor from the last key returned here.
	public async Task<List<ClientLookupDTO>> GetClientPageKeysAsync(string? searchTerm, string? afterClientName, int? afterClientId, int take, CancellationToken cancellationToken)
	{
		var logical = BuildLogicalClientsQuery(searchTerm);
		if (afterClientName is not null && afterClientId.HasValue)
		{
			var cId = afterClientId.Value;
			logical = logical.Where(client =>
				string.Compare(client.ClientName, afterClientName) > 0
				|| (client.ClientName == afterClientName && client.ClientId > cId));
		}

		return await logical.OrderBy(client => client.ClientName).ThenBy(client => client.ClientId)
			.Take(take).ToListAsync(cancellationToken);
	}

	public Task<List<ClientDetailsDTO>> GetClientsByIdsAsync(IReadOnlyCollection<int> clientIds, string? searchTerm, CancellationToken cancellationToken) =>
		BuildClientsQuery(searchTerm).Where(client => clientIds.Contains(client.ClientId)).OrderBy(client => client.ClientName)
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

	public Task<long> CountClientsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildLogicalClientsQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<ClientDetails> BuildClientsQuery(string? searchTerm)
	{
		var query = _dbcontext.ClientDetails.AsNoTracking();
		if (!string.IsNullOrEmpty(searchTerm))
		{
			var term = $"%{searchTerm}%";
			query = query.Where(client => EF.Functions.ILike(client.ClientName, term) || EF.Functions.ILike(client.ClientDescription, term));
		}
		return query;
	}

	private IQueryable<ClientLookupDTO> BuildLogicalClientsQuery(string? searchTerm) =>
		BuildClientsQuery(searchTerm).GroupBy(client => client.ClientId).Select(group => new ClientLookupDTO
		{
			ClientId = group.Key,
			ClientName = group.Min(client => client.ClientName)
		});

	public async Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var clients = clientDTOs.ToArray();
		var clientName = clients[0].ClientName.Trim();

		var now = DateTime.UtcNow;
		await using var transaction = await _dbcontext.Database.BeginTransactionAsync(cancellationToken);
		var first = new ClientDetails
		{
			ClientName = clientName,
			ClientDescription = clients[0].ClientDescription.Trim(),
			IsActive = clients[0].IsActive,
			PackageId = clients[0].PackageId,
			CreatedAt = now,
			UpdatedAt = now
		};
		await _dbcontext.ClientDetails.AddAsync(first, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		await _dbcontext.ClientDetails.AddRangeAsync(clients.Skip(1).Select(client => new ClientDetails
		{
			ClientId = first.ClientId,
			ClientName = clientName,
			ClientDescription = clients[0].ClientDescription.Trim(),
			IsActive = clients[0].IsActive,
			PackageId = client.PackageId,
			CreatedAt = now,
			UpdatedAt = now
		}), cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken) =>
		await _dbcontext.ClientDetails.AsNoTracking().Where(client => client.ClientId == clientId)
			.OrderBy(client => client.PackageId).ToListAsync(cancellationToken);

	public Task<bool> ClientNameExistsAsync(string clientName, int? excludeClientId, CancellationToken cancellationToken) =>
		_dbcontext.ClientDetails.AsNoTracking()
			.AnyAsync(client => (!excludeClientId.HasValue || client.ClientId != excludeClientId.Value)
				&& EF.Functions.ILike(client.ClientName, clientName), cancellationToken);

	public Task<int> CountActivePackagesAsync(IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken) =>
		_dbcontext.PackageDetails.AsNoTracking()
			.CountAsync(package => packageIds.Contains(package.PackageId) && package.IsActive, cancellationToken);

	public async Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var clients = clientDTOs.ToArray();
		var clientId = clients[0].ClientId;
		var existing = await _dbcontext.ClientDetails.Where(client => client.ClientId == clientId).ToListAsync(cancellationToken);

		var name = clients[0].ClientName.Trim();
		var selectedPackageIds = clients.Select(client => client.PackageId).Distinct().ToHashSet();
		var existingPackageIds = existing.Select(client => client.PackageId).ToHashSet();
		var newPackageIds = selectedPackageIds.Except(existingPackageIds).ToArray();

		var now = DateTime.UtcNow;
		var createdAt = existing.Min(client => client.CreatedAt);
		var description = clients[0].ClientDescription.Trim();
		var isActive = clients[0].IsActive;
		_dbcontext.ClientDetails.RemoveRange(existing.Where(client => !selectedPackageIds.Contains(client.PackageId)));
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
		await _dbcontext.ClientDetails.AddRangeAsync(added, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return existing.Where(client => selectedPackageIds.Contains(client.PackageId)).Concat(added).OrderBy(client => client.PackageId).ToArray();
	}
}
