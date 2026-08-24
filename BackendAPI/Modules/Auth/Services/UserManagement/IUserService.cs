namespace Auth.Services;

public interface IUserService
{
	Task<KeysetPaginatedResult<UsersDTO>> GetUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<KeysetPaginatedResult<UsersDTO>> GetUnApprovedUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<UserDTO> EditUserAsync(EditUserDTO userDTO);

	Task<bool> SendApprovalToUserEmailAsync(string Gmail);
}
