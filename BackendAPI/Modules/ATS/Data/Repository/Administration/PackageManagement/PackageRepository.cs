namespace ATS.Data.Repository.Administration.PackageManagement;

public sealed class PackageRepository : IPackageRepository
{
	private readonly ATSDBContext _dbContext;

	public PackageRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest request, int? clientId, CancellationToken cancellationToken)
	{
		var query = _dbContext.PackageDetails.AsNoTracking().OrderBy(package => package.PackageName);
		var count = await query.CountAsync(cancellationToken);
		var items = await query
			.Join(_dbContext.ClientDetails, package => package.PackageId, clientPackage => clientPackage.PackageId, (package, clientPackage) => new { package, clientPackage })
			.Where(joined => joined.clientPackage.ClientId == clientId)
			.Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
			.Select(package => new PackageDetailsDTO
			{
				PackageId = package.clientPackage.PackageId,
				PackageName = package.package.PackageName,
				PackageDescription = package.package.PackageDescription,
				IsActive = package.package.IsActive,
				FollowUpEmail = package.package.FollowUpEmail,
				CreatedAt = package.package.CreatedAt,
				UpdatedAt = package.package.UpdatedAt
			}).ToListAsync(cancellationToken);
		return new PaginatedResult<PackageDetailsDTO>(request.PageIndex, request.PageSize, count, items);
	}

	public async Task<PaginatedResult<PackageDetailsDTO>> SearchPackagesAsync(PaginationRequest request, int? clientId, CancellationToken cancellationToken)
	{
		var query = _dbContext.PackageDetails.AsNoTracking().Where(package =>
			EF.Functions.ILike(package.PackageName, $"%{request.SearchTerm}%") ||
			EF.Functions.ILike(package.PackageDescription, $"%{request.SearchTerm}%"));
		var count = await query.CountAsync(cancellationToken);
		var items = await query.OrderBy(package => package.PackageName)
			.Join(_dbContext.ClientDetails, 
				package => package.PackageId, 
				clientPackage => clientPackage.PackageId, 
				(package, clientPackage) => new { package, clientPackage })
			.Where(joined => joined.clientPackage.ClientId == clientId)
			.Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
			.Select(package => new PackageDetailsDTO
			{
				PackageId = package.clientPackage.PackageId,
				PackageName = package.package.PackageName,
				PackageDescription = package.package.PackageDescription,
				IsActive = package.package.IsActive,
				FollowUpEmail = package.package.FollowUpEmail,
				CreatedAt = package.package.CreatedAt,
				UpdatedAt = package.package.UpdatedAt
			}).ToListAsync(cancellationToken);
		return new PaginatedResult<PackageDetailsDTO>(request.PageIndex, request.PageSize, count, items);
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

