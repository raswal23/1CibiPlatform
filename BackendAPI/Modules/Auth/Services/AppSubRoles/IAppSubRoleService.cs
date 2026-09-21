namespace Auth.Services;

public interface IAppSubRoleService
{
	Task<KeysetPaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteAppSubRoleAsync(int AppSubRoleId);

	Task<AppSubRoleDTO> EditAppSubRoleAsync(EditAppSubRoleDTO appSubRoleDTO, CancellationToken cancellationToken);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole, CancellationToken cancellationToken);

	Task<bool> SendToUserEmailAsync(AccountNotificationDTO accountNotificationDTO);
}
