namespace FrontendWebassembly.Services.ATS.Interface;

public interface IPackageManagementService
{
	Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO);
	Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO);
}
