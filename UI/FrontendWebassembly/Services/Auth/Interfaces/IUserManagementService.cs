namespace FrontendWebassembly.Services.Auth.Interfaces;

public interface IUserManagementService
{
	Task<ServiceResponse<KeysetPaginatedResult<UsersDTO>>> GetUsersAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<KeysetPaginatedResult<UnApprovedUsersDTO>>> GetUnApprovedUsersAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken ct = default);
	Task<ServiceResponse<KeysetPaginatedResult<LockedUsersDTO>>> GetLockedUsersAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken ct = default);
	Task<ServiceResponse<KeysetPaginatedResult<ApplicationsDTO>>> GetApplicationsAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<KeysetPaginatedResult<SubMenusDTO>>> GetSubMenusAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<KeysetPaginatedResult<RolesDTO>>> GetRolesAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<KeysetPaginatedResult<AppSubRolesDTO>>> GetAppSubRolesAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);

	Task<ServiceResponse<bool>> DeleteApplicationAsync(int AppId);
	Task<ServiceResponse<bool>> DeleteSubMenuAsync(int SubMenuId);
	Task<ServiceResponse<bool>> DeleteRoleAsync(int RoleId);
	Task<ServiceResponse<bool>> DeleteUserAppSubRoleAsync(int AppSubRoleId);
	Task<ServiceResponse<bool>> DeleteLockedUserAsync(Guid lockedUserId);

	Task<ServiceResponse<bool>> AddApplicationAsync(AddApplicationDTO addApplicationDTO);
	Task<ServiceResponse<bool>> AddSubMenuAsync(AddSubMenuDTO addSubMenuDTO);
	Task<ServiceResponse<bool>> AddRoleAsync(AddRoleDTO addRoleDTO);
	Task<ServiceResponse<bool>> AddAppSubRoleAsync(AddAppSubRoleDTO addAppSubRoleDTO);

	Task<ServiceResponse<EditUserDTO>> EditUserAsync(UnApprovedUsersDTO editUserDTO);
	Task<ServiceResponse<EditApplicationDTO>> EditApplicationAsync(ApplicationsDTO editApplicationDTO);
	Task<ServiceResponse<EditSubMenuDTO>> EditSubMenuAsync(SubMenusDTO editSubMenuDTO);
	Task<ServiceResponse<EditRoleDTO>> EditRoleAsync(RolesDTO editRoleDTO);
	Task<ServiceResponse<AppSubRoleDTO>> EditAppSubRoleAsync(EditAppSubRoleDTO editAppSubRoleDTO);

	Task<ServiceResponse<bool>> SendNotificationAsync(AssignmentNotificationDTO accountNotificationDTO);
	Task<ServiceResponse<bool>> SendApprovalNotificationAsync(string Gmail);
}
