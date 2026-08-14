namespace Auth.Data.Repository;

public interface IAppSubRoleRepository
{
	Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<AuthUserAppRole> GetAppSubRoleAsync(int appSubRoleId);
	Task<PaginatedResult<AppSubRolesDTO>> SearchAppSubRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole);
	Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole);
	Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole);
}
