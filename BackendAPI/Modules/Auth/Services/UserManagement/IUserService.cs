namespace Auth.Services;

public interface IUserService
{
	Task<PaginatedResult<UsersDTO>> GetUsersAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<PaginatedResult<UsersDTO>> GetUnApprovedUsersAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<UserDTO> EditUserAsync(EditUserDTO userDTO);

	Task<bool> SendApprovalToUserEmailAsync(string Gmail);
}
