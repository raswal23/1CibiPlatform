namespace ATS.Data.Repository.Administration;

public interface IPackageRepository
{
	Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<PackageDetailsDTO>> SearchPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken);
	Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken);
	Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken);
}
