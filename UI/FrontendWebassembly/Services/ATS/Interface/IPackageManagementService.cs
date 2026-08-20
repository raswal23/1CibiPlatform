namespace FrontendWebassembly.Services.ATS.Interface;

public interface IPackageManagementService
{
	Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default, int? clientId = null);
	Task<IReadOnlyList<PackageDetailsDTO>> GetAllPackagesAsync(CancellationToken cancellationToken = default, int? clientId = null);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken = default);
	Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken = default);
}
