namespace FrontendWebassembly.Services.Auth.Interfaces;

public interface IUserManagementService
{
	Task<ServiceResponse<PaginatedResult<UsersDTO>>> GetUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<PaginatedResult<UnApprovedUsersDTO>>> GetUnApprovedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default);
	Task<ServiceResponse<PaginatedResult<LockedUsersDTO>>> GetLockedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default);
	Task<ServiceResponse<PaginatedResult<ApplicationsDTO>>> GetApplicationsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<PaginatedResult<SubMenusDTO>>> GetSubMenusAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<PaginatedResult<RolesDTO>>> GetRolesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<PaginatedResult<AppSubRolesDTO>>> GetAppSubRolesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);

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
	Task<ServiceResponse<EditAppSubRoleDTO>> EditAppSubRoleAsync(AppSubRolesDTO editAppSubRoleDTO);

	Task<ServiceResponse<bool>> SendNotificationAsync(AssignmentNotificationDTO accountNotificationDTO);
	Task<ServiceResponse<bool>> SendApprovalNotificationAsync(string Gmail);
}
