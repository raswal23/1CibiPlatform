namespace ATS.Services.Settings.RoleManagement;

public interface IRoleManagementService
{
	Task<KeysetPaginatedResult<RoleDetailsDTO>> GetRolesAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddRoleAsync(AddRoleDTO roleDTO);
	Task<RoleDetailsDTO> EditRoleAsync(EditRoleDTO roleDTO);
}
