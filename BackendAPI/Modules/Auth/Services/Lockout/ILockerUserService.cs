namespace Auth.Services;

public interface ILockerUserService
{
	Task<KeysetPaginatedResult<AuthAttempts>> GetLockedUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteLockedUserAsync(Guid lockedUserId);
}
