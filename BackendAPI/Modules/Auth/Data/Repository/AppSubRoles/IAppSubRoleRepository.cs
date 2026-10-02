namespace Auth.Data.Repository;

public interface IAppSubRoleRepository
{
	Task<List<AppSubRolesDTO>> GetAppSubRolesPageAsync(string? searchTerm, int? afterAppRoleId, int take, CancellationToken cancellationToken);
	Task<long> CountAppSubRolesAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<AuthUserAppRole?> GetAppSubRoleAsync(int appSubRoleId);

	/// <summary>
	/// Whether this user already holds an assignment on the same application and submenu.
	/// Pass <paramref name="excludeAppRoleId"/> on the edit path so a row does not collide
	/// with itself.
	/// </summary>
	Task<bool> AppSubRoleExistsAsync(
		Guid userId,
		int appId,
		int subMenuId,
		int? excludeAppRoleId,
		CancellationToken cancellationToken);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole);
	Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole);
	Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole);
}
