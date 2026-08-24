namespace Auth.Data.Repository;

public interface ILockoutRepository
{
	Task<List<AuthAttempts>> GetLockedUsersPageAsync(string? searchTerm, Guid? afterUserId, int take, CancellationToken cancellationToken);
	Task<long> CountLockedUsersAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<AuthAttempts> GetLockedUserAsync(Guid userId);
	Task<bool> DeleteLockedUserAsync(AuthAttempts authAttempts);
	Task<bool> SaveLockedUserAsync(AuthAttempts userAttempt);
}
