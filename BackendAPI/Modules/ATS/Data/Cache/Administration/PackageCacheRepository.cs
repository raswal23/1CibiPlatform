namespace ATS.Data.Cache.Administration;

public sealed class PackageCacheRepository : IPackageRepository
{
	private readonly IPackageRepository _repository;
	private readonly HybridCache _cache;

	public PackageCacheRepository(IPackageRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, string? afterPackageName, int take, CancellationToken cancellationToken)
	{
		if (afterPackageName is not null)
			return _repository.GetPackagesPageAsync(searchTerm, clientId, afterPackageName, take, cancellationToken);

		var key = $"package_v4_client_{clientId?.ToString() ?? "all"}_first_take_{take}_search_{searchTerm}";
		return _cache.GetOrCreateAsync<List<PackageDetailsDTO>>(
			key, async token => await _repository.GetPackagesPageAsync(searchTerm, clientId, null, take, token),
			tags: [CacheTags.Package], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountPackagesAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		_cache.GetOrCreateAsync<long>(
			$"package_v4_client_{clientId?.ToString() ?? "all"}_count_search_{searchTerm}",
			async token => await _repository.CountPackagesAsync(searchTerm, clientId, token),
			tags: [CacheTags.Package], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var result = await _repository.AddPackageAsync(packageDTO, cancellationToken);
		if (result)
			await _cache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}

	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_repository.GetPackageAsync(packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken)
	{
		var result = await _repository.EditPackageAsync(packageDetails, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.Client, cancellationToken);
		return result;
	}
}
