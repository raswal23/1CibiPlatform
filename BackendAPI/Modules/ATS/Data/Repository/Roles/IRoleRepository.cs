namespace ATS.Data.Repository;

public interface IRoleRepository
{
	Task<List<RoleDetailsDTO>> GetRolesPageAsync(string? searchTerm, string? afterRoleName, int take, CancellationToken cancellationToken);
	Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<bool> AddRoleAsync(AddRoleDTO roleDTO);
	Task<RoleDetails?> GetRoleAsync(int roleId);
	Task<RoleDetails> EditRoleAsync(RoleDetails roleDetails);
}
