namespace Auth.Data.Repository;

public interface IRoleRepository
{
	Task<List<RolesDTO>> GetRolesPageAsync(string? searchTerm, int? afterRoleId, int take, CancellationToken cancellationToken);
	Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<AuthRole> GetRoleAsync(int roleId);
	Task<bool> AddRoleAsync(AddRoleDTO role);
	Task<AuthRole> EditRoleAsync(AuthRole role);
	Task<bool> DeleteRoleAsync(AuthRole role);
}
