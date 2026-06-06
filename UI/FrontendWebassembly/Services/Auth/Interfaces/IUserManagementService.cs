namespace FrontendWebassembly.Services.Auth.Interfaces;

public interface IUserManagementService
{
	Task<PaginatedResult<UsersDTO>> GetUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<PaginatedResult<UnApprovedUsersDTO>> GetUnApprovedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default);
	Task<PaginatedResult<LockedUsersDTO>> GetLockedUsersAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken ct = default);
	Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<PaginatedResult<RolesDTO>> GetRolesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);
	Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, CancellationToken cancellationToken = default);

	Task<bool> DeleteApplicationAsync(int AppId);
	Task<bool> DeleteSubMenuAsync(int SubMenuId);
	Task<bool> DeleteRoleAsync(int RoleId);
	Task<bool> DeleteUserAppSubRoleAsync(int AppSubRoleId);
	Task<bool> DeleteLockedUserAsync(Guid lockedUserId);

	Task<bool> AddApplicationAsync(AddApplicationDTO addApplicationDTO);
	Task<bool> AddSubMenuAsync(AddSubMenuDTO addSubMenuDTO);
	Task<bool> AddRoleAsync(AddRoleDTO addRoleDTO);
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO addAppSubRoleDTO);

	Task<EditUserDTO> EditUserAsync(UnApprovedUsersDTO editUserDTO);
	Task<EditApplicationDTO> EditApplicationAsync(ApplicationsDTO editApplicationDTO);
	Task<EditSubMenuDTO> EditSubMenuAsync(SubMenusDTO editSubMenuDTO);
	Task<EditRoleDTO> EditRoleAsync(RolesDTO editRoleDTO);
	Task<EditAppSubRoleDTO> EditAppSubRoleAsync(AppSubRolesDTO editAppSubRoleDTO);

	Task<bool> SendNotificationAsync(AssignmentNotificationDTO accountNotificationDTO);
	Task<bool> SendApprovalNotificationAsync(string Gmail);
}
