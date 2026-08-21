namespace ATS.Data.Repository;

public interface IModuleRepository
{
	Task<List<ModuleDetailsDTO>> GetModulesPageAsync(string? searchTerm, string? afterModuleName, int take, CancellationToken cancellationToken);
	Task<long> CountModulesAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<bool> AddModuleAsync(AddModuleDTO moduleDTO);
	Task<bool> ModuleNameExistsAsync(string moduleName);
	Task<ModuleDetails?> GetModuleAsync(int moduleId);
	Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails);
}
