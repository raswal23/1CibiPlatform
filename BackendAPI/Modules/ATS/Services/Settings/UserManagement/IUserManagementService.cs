namespace ATS.Services.Settings.UserManagement;

public interface IUserManagementService
{
	Task<IReadOnlyList<ATSUserLookupDTO>> GetAuthUsersAsync(
		CancellationToken cancellationToken);

	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken);

	Task<UserClientDetailsDTO> AssignUserClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken);

	Task<KeysetPaginatedResult<UserDetailsDTO>> GetUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<int?> GetCurrentUserRoleIdAsync(CancellationToken cancellationToken);

	Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(
		Guid userId,
		CancellationToken cancellationToken);

	Task<bool> AddUserAsync(
		IReadOnlyCollection<AddUserDTO> userDTOs,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<UserDetailsDTO>> EditUserAsync(
		IReadOnlyCollection<EditUserDTO> userDTOs,
		CancellationToken cancellationToken);
}
