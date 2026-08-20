namespace FrontendWebassembly.Services.ATS.PackageManagement;

public interface IPackageManagementService
{
	Task<ServiceResponse<KeysetPaginatedResult<PackageDetailsDTO>>> GetPackagesAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default, int? clientId = null);
	Task<ServiceResponse<IReadOnlyList<PackageDetailsDTO>>> GetAllPackagesAsync(CancellationToken cancellationToken = default, int? clientId = null);
	Task<ServiceResponse<bool>> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken = default);
	Task<ServiceResponse<PackageDetailsDTO>> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken = default);
}
