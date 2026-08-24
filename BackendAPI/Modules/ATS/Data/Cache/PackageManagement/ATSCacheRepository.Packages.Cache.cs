namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, string? afterPackageName, int take, CancellationToken cancellationToken)
	{
		if (afterPackageName is not null)
			return _atsRepository.GetPackagesPageAsync(searchTerm, clientId, afterPackageName, take, cancellationToken);

		var key = $"package_v4_client_{clientId?.ToString() ?? "all"}_first_take_{take}_search_{searchTerm}";
		return _hybridCache.GetOrCreateAsync<List<PackageDetailsDTO>>(
			key, async token => await _atsRepository.GetPackagesPageAsync(searchTerm, clientId, null, take, token),
			tags: [CacheTags.Package], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountPackagesAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<long>(
			$"package_v4_client_{clientId?.ToString() ?? "all"}_count_search_{searchTerm}",
			async token => await _atsRepository.CountPackagesAsync(searchTerm, clientId, token),
			tags: [CacheTags.Package], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AddPackageAsync(packageDTO, cancellationToken);
		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}

	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_atsRepository.GetPackageAsync(packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.EditPackageAsync(packageDetails, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Client, cancellationToken);
		return result;
	}
}
