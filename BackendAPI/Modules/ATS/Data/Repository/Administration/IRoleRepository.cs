namespace ATS.Data.Repository.Administration;

public interface IRoleRepository
{
	Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<RoleDetailsDTO>> SearchRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddRoleAsync(AddRoleDTO roleDTO);
	Task<RoleDetails?> GetRoleAsync(int roleId);
	Task<RoleDetails> EditRoleAsync(RoleDetails roleDetails);
}
