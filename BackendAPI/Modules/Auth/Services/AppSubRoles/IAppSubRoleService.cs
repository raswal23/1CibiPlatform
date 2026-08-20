namespace Auth.Services;

public interface IAppSubRoleService
{
	Task<KeysetPaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteAppSubRoleAsync(int AppSubRoleId);

	Task<AppSubRoleDTO> EditAppSubRoleAsync(EditAppSubRoleDTO appSubRoleDTO);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole);

	Task<bool> SendToUserEmailAsync(AccountNotificationDTO accountNotificationDTO);
}
