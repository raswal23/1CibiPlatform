namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<AuthAttempts>> GetLockedUsersPageAsync(string? searchTerm, Guid? afterUserId, int take, CancellationToken cancellationToken)
		{
			if (afterUserId.HasValue)
				return await _authRepository.GetLockedUsersPageAsync(searchTerm, afterUserId, take, cancellationToken);

			var cacheKey = $"lockedusers_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<AuthAttempts>>(
				cacheKey,
				async token => await _authRepository.GetLockedUsersPageAsync(searchTerm, null, take, token),
				tags: [LockedUsersTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountLockedUsersAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"lockedusers_count_search_{searchTerm}",
				async token => await _authRepository.CountLockedUsersAsync(searchTerm, token),
				tags: [LockedUsersTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<AuthAttempts> GetLockedUserAsync(Guid userId)
		{
			var cacheKey = $"{UserLockoutDate}_{userId}";
	
			return await _hybridCache.GetOrCreateAsync<AuthAttempts>(
				cacheKey,
				async (token) => await _authRepository.GetLockedUserAsync(userId),
				tags: [UserLockoutDate]);
		}
	
	public async Task<bool> DeleteLockedUserAsync(AuthAttempts lockedUser)
		{
			var cachekeyForDate = $"{UserLockoutDate}_{lockedUser.UserId}";
			var cacheKeyForAttempt = $"{_userAttemptTag}_{lockedUser.UserId}";
	
			var result = await _authRepository.DeleteLockedUserAsync(lockedUser);
	
			if (result)
			{
				await _hybridCache.RemoveAsync(cacheKeyForAttempt);
				await _hybridCache.RemoveByTagAsync(LockedUsersTag);
				await _hybridCache.RemoveAsync(cachekeyForDate);
			}
	
			return result;
		}
	
	public async Task<bool> SaveLockedUserAsync(AuthAttempts userAttempt)
		{
			var result = await _authRepository.SaveLockedUserAsync(userAttempt);
			var cachekeyForDate = $"{UserLockoutDate}_{userAttempt.UserId}";
	
			if (result)
			{
				await _hybridCache.RemoveByTagAsync(LockedUsersTag);
				await _hybridCache.RemoveAsync(cachekeyForDate);
			}
	
			return result;
		}
}
