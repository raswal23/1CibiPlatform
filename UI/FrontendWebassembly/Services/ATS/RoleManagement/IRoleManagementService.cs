namespace FrontendWebassembly.Services.ATS.RoleManagement;

public interface IRoleManagementService
{
	Task<ServiceResponse<PaginatedResult<RoleDetailsDTO>>> GetRolesAsync(
		int? pageNumber = 1,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<IReadOnlyList<RoleDetailsDTO>>> GetAllRolesAsync(CancellationToken cancellationToken = default);

	Task<ServiceResponse<bool>> AddRoleAsync(AddATSRoleDTO roleDTO, CancellationToken cancellationToken = default);

	Task<ServiceResponse<RoleDetailsDTO>> EditRoleAsync(EditATSRoleDTO roleDTO, CancellationToken cancellationToken = default);
}
