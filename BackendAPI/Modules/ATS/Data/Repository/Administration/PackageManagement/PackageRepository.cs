namespace ATS.Data.Repository.Administration.PackageManagement;

public sealed class PackageRepository : IPackageRepository
{
	private readonly ATSDBContext _dbContext;

	public PackageRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	// Keyset ordered by PackageName (unique index) — pure query; the service decodes
	// the cursor and mints the next one.
	public async Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, string? afterPackageName, int take, CancellationToken cancellationToken)
	{
		var query = BuildQuery(searchTerm, clientId);
		if (afterPackageName is not null)
			query = query.Where(package => string.Compare(package.PackageName, afterPackageName) > 0);

		return await query.OrderBy(package => package.PackageName).Take(take)
			.Select(package => new PackageDetailsDTO
			{
				PackageId = package.PackageId,
				PackageName = package.PackageName,
				PackageDescription = package.PackageDescription,
				IsActive = package.IsActive,
				FollowUpEmail = package.FollowUpEmail,
				CreatedAt = package.CreatedAt,
				UpdatedAt = package.UpdatedAt
			}).ToListAsync(cancellationToken);
	}

	public Task<long> CountPackagesAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		BuildQuery(searchTerm, clientId).LongCountAsync(cancellationToken);

	private IQueryable<PackageDetails> BuildQuery(string? searchTerm, int? clientId)
	{
		var query = _dbContext.PackageDetails.AsNoTracking();
		if (clientId is > 0)
			query = query.Where(package => _dbContext.ClientDetails.Any(client =>
				client.ClientId == clientId.Value && client.PackageId == package.PackageId));
		if (!string.IsNullOrEmpty(searchTerm))
			query = query.Where(package =>
				EF.Functions.ILike(package.PackageName, $"%{searchTerm}%") ||
				EF.Functions.ILike(package.PackageDescription, $"%{searchTerm}%"));
		return query;
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO dto, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		await _dbContext.PackageDetails.AddAsync(new PackageDetails
		{
			PackageName = dto.PackageName.Trim(),
			PackageDescription = dto.PackageDescription.Trim(),
			IsActive = dto.IsActive,
			FollowUpEmail = dto.FollowUpEmail,
			CreatedAt = now,
			UpdatedAt = now
		}, cancellationToken);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_dbContext.PackageDetails.AsNoTracking().FirstOrDefaultAsync(package => package.PackageId == packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails package, CancellationToken cancellationToken)
	{
		_dbContext.PackageDetails.Update(package);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return package;
	}
}

