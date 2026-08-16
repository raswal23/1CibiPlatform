namespace Auth.Data.Repository;

public interface ILockoutRepository
{
	Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<AuthAttempts> GetLockedUserAsync(Guid userId);
	Task<PaginatedResult<AuthAttempts>> SearchLockedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> DeleteLockedUserAsync(AuthAttempts authAttempts);
	Task<bool> SaveLockedUserAsync(AuthAttempts userAttempt);
}
