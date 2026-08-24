namespace ATS.Services.Settings.PackageManagement;

public interface IPackageManagementService
{
	Task<KeysetPaginatedResult<PackageDetailsDTO>> GetPackagesAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken, int? clientId = null);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken);
	Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken);
}
