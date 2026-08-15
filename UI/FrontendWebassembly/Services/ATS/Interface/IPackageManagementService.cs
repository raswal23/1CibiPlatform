namespace FrontendWebassembly.Services.ATS.Interface;

public interface IPackageManagementService
{
	Task<ServiceResponse<PaginatedResult<PackageDetailsDTO>>> GetPackagesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default, int? clientId = null);
	Task<ServiceResponse<IReadOnlyList<PackageDetailsDTO>>> GetAllPackagesAsync(CancellationToken cancellationToken = default, int? clientId = null);
	Task<ServiceResponse<bool>> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken = default);
	Task<ServiceResponse<PackageDetailsDTO>> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken = default);
}
