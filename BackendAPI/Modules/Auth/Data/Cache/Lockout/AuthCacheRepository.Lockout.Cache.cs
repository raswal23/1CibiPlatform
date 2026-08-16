namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"lockedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AuthAttempts>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetLockedUsersAsync(req, token),
				tags: [LockedUsersTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<PaginatedResult<AuthAttempts>> SearchLockedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"lockedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AuthAttempts>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchLockedUserAsync(req, token),
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
