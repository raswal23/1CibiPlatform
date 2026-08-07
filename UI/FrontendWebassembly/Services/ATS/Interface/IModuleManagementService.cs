namespace FrontendWebassembly.Services.ATS.Interface;

public interface IModuleManagementService
{
	Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(
		int? pageNumber = 1,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<bool> AddModuleAsync(AddATSModuleDTO moduleDTO, CancellationToken cancellationToken = default);

	Task<ModuleDetailsDTO> EditModuleAsync(EditATSModuleDTO moduleDTO, CancellationToken cancellationToken = default);
}
