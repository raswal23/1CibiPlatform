namespace ATS.Services;

public interface IPackageManagementService
{
	Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken, int? clientId = null);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken);
	Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken);
}
