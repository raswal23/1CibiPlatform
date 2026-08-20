namespace FrontendWebassembly.Services.ATS.Interface;

public interface IModuleManagementService
{
	Task<ServiceResponse<KeysetPaginatedResult<ModuleDetailsDTO>>> GetModulesAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<IReadOnlyList<ModuleDetailsDTO>>> GetAllModulesAsync(CancellationToken cancellationToken = default);

	Task<ServiceResponse<bool>> AddModuleAsync(AddATSModuleDTO moduleDTO, CancellationToken cancellationToken = default);

	Task<ServiceResponse<ModuleDetailsDTO>> EditModuleAsync(EditATSModuleDTO moduleDTO, CancellationToken cancellationToken = default);
}
