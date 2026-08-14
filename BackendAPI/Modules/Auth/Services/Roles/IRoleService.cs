namespace Auth.Services;
public interface IRoleService
{
	Task<PaginatedResult<RolesDTO>> GetRolesAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteRoleAsync(int RoleId);

	Task<RoleDTO> EditRoleAsync(EditRoleDTO roleDTO);
	Task<bool> AddRoleAsync(AddRoleDTO role);
}
