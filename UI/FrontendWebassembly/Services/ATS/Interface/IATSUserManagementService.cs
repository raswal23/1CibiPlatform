namespace FrontendWebassembly.Services.ATS.Interface;

public interface IATSUserManagementService
{
	Task<IReadOnlyList<ATSUserLookupDTO>> GetAuthUsersAsync(
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken = default);

	Task<UserClientDetailsDTO> AssignUserClientAsync(
		AssignATSUserClientDTO assignmentDTO,
		CancellationToken cancellationToken = default);

	Task<GetUsersResponseDTO> GetUsersAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<int>> GetMyModuleIdsAsync(
		CancellationToken cancellationToken = default);

	Task<bool> AddUserAsync(AddATSUserDTO userDTO, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<UserDetailsDTO>> EditUserAsync(EditATSUserDTO userDTO, CancellationToken cancellationToken = default);
}
