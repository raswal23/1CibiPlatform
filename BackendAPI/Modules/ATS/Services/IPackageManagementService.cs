namespace ATS.Services;

public interface IPackageManagementService
{
	Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO);
	Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO);
}
