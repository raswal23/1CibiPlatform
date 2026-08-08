namespace ATS.Data.Cache.Administration;

public sealed class PackageCacheRepository : IPackageRepository
{
	private const string PackageTag = "package";
	private readonly IPackageRepository _repository;
	private readonly HybridCache _cache;

	public PackageCacheRepository(IPackageRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	public Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"package_v3_page_{request.PageIndex}_size_{request.PageSize}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<PackageDetailsDTO>>(
			key, request, async (value, token) => await _repository.GetPackagesAsync(value, token), null,
			tags: [PackageTag], cancellationToken: cancellationToken).AsTask();
	}

	public Task<PaginatedResult<PackageDetailsDTO>> SearchPackagesAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"package_v3_page_{request.PageIndex}_size_{request.PageSize}_search_{request.SearchTerm}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<PackageDetailsDTO>>(
			key, request, async (value, token) => await _repository.SearchPackagesAsync(value, token), null,
			tags: [PackageTag], cancellationToken: cancellationToken).AsTask();
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var result = await _repository.AddPackageAsync(packageDTO, cancellationToken);
		if (result)
			await _cache.RemoveByTagAsync(PackageTag, cancellationToken);
		return result;
	}

	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_repository.GetPackageAsync(packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken)
	{
		var result = await _repository.EditPackageAsync(packageDetails, cancellationToken);
		await _cache.RemoveByTagAsync(PackageTag, cancellationToken);
		return result;
	}
}
