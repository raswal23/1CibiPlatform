namespace ATS.Services.Settings.ModuleManagement;

public interface IModuleManagementService
{
	Task<KeysetPaginatedResult<ModuleDetailsDTO>> GetModulesAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddModuleAsync(AddModuleDTO moduleDTO);
	Task<ModuleDetailsDTO> EditModuleAsync(EditModuleDTO moduleDTO);
}
