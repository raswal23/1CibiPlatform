namespace ATS.Data.Repository;

public interface IPackageRepository
{
	Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, string? afterPackageName, int take, CancellationToken cancellationToken);
	Task<long> CountPackagesAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken);
	Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken);
	Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken);
}
