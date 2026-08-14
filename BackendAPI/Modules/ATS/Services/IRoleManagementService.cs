namespace ATS.Services;

public interface IRoleManagementService
{
	Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddRoleAsync(AddRoleDTO roleDTO);
	Task<RoleDetailsDTO> EditRoleAsync(EditRoleDTO roleDTO);
}
