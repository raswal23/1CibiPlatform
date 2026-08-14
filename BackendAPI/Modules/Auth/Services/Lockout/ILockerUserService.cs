namespace Auth.Services;

public interface ILockerUserService
{
	Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteLockedUserAsync(Guid lockedUserId);
}
