namespace Auth.Data.Repository;

public interface IRoleRepository
{
	Task<PaginatedResult<RolesDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<AuthRole> GetRoleAsync(int roleId);
	Task<PaginatedResult<RolesDTO>> SearchRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddRoleAsync(AddRoleDTO role);
	Task<AuthRole> EditRoleAsync(AuthRole role);
	Task<bool> DeleteRoleAsync(AuthRole role);
}
