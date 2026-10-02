namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset ordered by PackageName (unique index) — pure query; the service decodes
	// the cursor and mints the next one.
	public async Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, bool? autoChasing, string? afterPackageName, int take, CancellationToken cancellationToken)
	{
		var query = BuildPackagesQuery(searchTerm, clientId, autoChasing);
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
				AutoChasing = package.AutoChasing,
				CreatedAt = package.CreatedAt,
				UpdatedAt = package.UpdatedAt
			}).ToListAsync(cancellationToken);
	}

	public Task<long> CountPackagesAsync(string? searchTerm, int? clientId, bool? autoChasing, CancellationToken cancellationToken) =>
		BuildPackagesQuery(searchTerm, clientId, autoChasing).LongCountAsync(cancellationToken);

	private IQueryable<PackageDetails> BuildPackagesQuery(string? searchTerm, int? clientId, bool? autoChasing)
	{
		var query = _dbcontext.PackageDetails.AsNoTracking();
		if (clientId is > 0)
			query = query.Where(package => _dbcontext.ClientDetails.Any(client =>
				client.ClientId == clientId.Value && client.PackageId == package.PackageId));
		// Filters to one screening type. Unclassified (null) packages match neither
		// Manual nor Data on purpose - null must never pass for Data.
		if (autoChasing is not null)
			query = query.Where(package => package.AutoChasing == autoChasing);
		if (!string.IsNullOrEmpty(searchTerm))
			query = query.Where(package =>
				EF.Functions.ILike(package.PackageName, $"%{searchTerm}%") ||
				EF.Functions.ILike(package.PackageDescription, $"%{searchTerm}%"));
		return query;
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO dto, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		await _dbcontext.PackageDetails.AddAsync(new PackageDetails
		{
			PackageName = dto.PackageName.Trim(),
			PackageDescription = dto.PackageDescription.Trim(),
			IsActive = dto.IsActive,
			FollowUpEmail = dto.FollowUpEmail,
			AutoChasing = dto.AutoChasing,
			CreatedAt = now,
			UpdatedAt = now
		}, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_dbcontext.PackageDetails.AsNoTracking().FirstOrDefaultAsync(package => package.PackageId == packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails package, CancellationToken cancellationToken)
	{
		_dbcontext.PackageDetails.Update(package);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return package;
	}

	// ClientDetails holds one row per (ClientId, PackageId), so a plain count over
	// one PackageId is already a count of logical clients.
	public Task<int> CountActiveClientsUsingPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_dbcontext.ClientDetails.AsNoTracking()
			.CountAsync(client => client.PackageId == packageId && client.IsActive, cancellationToken);

	public async Task<(int Orders, int BulkFiles)> RelabelPackageOnOrdersAsync(
		int packageId,
		string packageName,
		CancellationToken cancellationToken)
	{
		// NeedsProjection is raised alongside the label so ApplicantSearchProjectionService
		// rebuilds the denormalised search row, which copies SelectPackage into itself.
		var orders = await _dbcontext.EmailInvitationRequests
			.Where(invitation => invitation.PackageId == packageId
				&& invitation.SelectPackage != packageName)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.SelectPackage, x => packageName)
				.SetProperty(x => x.NeedsProjection, x => true),
				cancellationToken);

		var bulkFiles = await _dbcontext.BulkUploadFileDetails
			.Where(file => file.PackageId == packageId
				&& file.PackageType != packageName)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.PackageType, x => packageName),
				cancellationToken);

		return (orders, bulkFiles);
	}
}
