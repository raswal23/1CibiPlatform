namespace Auth.Services;

public interface IAppSubRoleService
{
	Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteAppSubRoleAsync(int AppSubRoleId);

	Task<AppSubRoleDTO> EditAppSubRoleAsync(EditAppSubRoleDTO appSubRoleDTO);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole);

	Task<bool> SendToUserEmailAsync(AccountNotificationDTO accountNotificationDTO);
}
