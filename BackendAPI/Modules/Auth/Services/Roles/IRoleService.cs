namespace Auth.Services;
public interface IRoleService
{
	Task<KeysetPaginatedResult<RolesDTO>> GetRolesAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteRoleAsync(int RoleId);

	Task<RoleDTO> EditRoleAsync(EditRoleDTO roleDTO);
	Task<bool> AddRoleAsync(AddRoleDTO role);
}
