namespace FrontendWebassembly.Services.ATS.Interface;

public interface IATSUserManagementService
{
	Task<ServiceResponse<IReadOnlyList<ATSUserLookupDTO>>> GetAuthUsersAsync(
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<IReadOnlyList<UserClientDetailsDTO>>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<UserClientDetailsDTO>> AssignUserClientAsync(
		AssignATSUserClientDTO assignmentDTO,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<GetUsersResponseDTO>> GetUsersAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<IReadOnlyList<int>>> GetMyModuleIdsAsync(
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<int?>> GetMyRoleIdAsync(
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<GetMyAtsAccessResponseDTO>> GetMyAtsAccessAsync(
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<bool>> AddUserAsync(AddATSUserDTO userDTO, CancellationToken cancellationToken = default);

	Task<ServiceResponse<IReadOnlyList<UserDetailsDTO>>> EditUserAsync(EditATSUserDTO userDTO, CancellationToken cancellationToken = default);
}
