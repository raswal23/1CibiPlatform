namespace Auth.Data.Repository;

public interface IAppSubRoleRepository
{
	Task<List<AppSubRolesDTO>> GetAppSubRolesPageAsync(string? searchTerm, int? afterAppRoleId, int take, CancellationToken cancellationToken);
	Task<long> CountAppSubRolesAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<AuthUserAppRole> GetAppSubRoleAsync(int appSubRoleId);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole);
	Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole);
	Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole);
}
