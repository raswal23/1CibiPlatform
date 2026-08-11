namespace ATS.Data.Repository.Administration.Modules;

public interface IModuleRepository
{
	Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<ModuleDetailsDTO>> SearchModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddModuleAsync(AddModuleDTO moduleDTO);
	Task<ModuleDetails?> GetModuleAsync(int moduleId);
	Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails);
}
