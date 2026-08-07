namespace ATS.Services;

public interface IModuleManagementService
{
	Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddModuleAsync(AddModuleDTO moduleDTO);
	Task<ModuleDetailsDTO> EditModuleAsync(EditModuleDTO moduleDTO);
}
