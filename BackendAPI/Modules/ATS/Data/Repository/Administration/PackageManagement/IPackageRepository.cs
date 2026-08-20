namespace ATS.Data.Repository.Administration.PackageManagement;

public interface IPackageRepository
{
	Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest paginationRequest, int? clientId, CancellationToken cancellationToken);
	Task<PaginatedResult<PackageDetailsDTO>> SearchPackagesAsync(PaginationRequest paginationRequest, int? clientId, CancellationToken cancellationToken);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken);
	Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken);
	Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken);
}
