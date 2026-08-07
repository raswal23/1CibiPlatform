namespace FrontendWebassembly.Services.ATS.Interface;

public interface IRoleManagementService
{
	Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(
		int? pageNumber = 1,
		int? pageSize = 10,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<bool> AddRoleAsync(AddATSRoleDTO roleDTO, CancellationToken cancellationToken = default);

	Task<RoleDetailsDTO> EditRoleAsync(EditATSRoleDTO roleDTO, CancellationToken cancellationToken = default);
}
