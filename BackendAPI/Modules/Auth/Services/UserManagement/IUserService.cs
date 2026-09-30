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

	/// <summary>
	/// Activates or deactivates a user account, changing nothing else about them.
	/// </summary>
	Task<UserDTO> EditUserStatusAsync(EditUserStatusDTO userStatusDTO);

	/// <summary>
	/// Rejects a user awaiting approval, taking them off the approval queue.
	/// </summary>
	Task<bool> RejectUserAsync(Guid userId);

	Task<bool> SendApprovalToUserEmailAsync(string Gmail);
}
