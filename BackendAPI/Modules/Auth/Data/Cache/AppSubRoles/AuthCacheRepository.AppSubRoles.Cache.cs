namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<AppSubRolesDTO>> GetAppSubRolesPageAsync(string? searchTerm, int? afterAppRoleId, int take, CancellationToken cancellationToken)
		{
			if (afterAppRoleId.HasValue)
				return await _authRepository.GetAppSubRolesPageAsync(searchTerm, afterAppRoleId, take, cancellationToken);

			var cacheKey = $"appsubroles_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<AppSubRolesDTO>>(
				cacheKey,
				async token => await _authRepository.GetAppSubRolesPageAsync(searchTerm, null, take, token),
				tags: [AppSubRolesTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountAppSubRolesAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"appsubroles_count_search_{searchTerm}",
				async token => await _authRepository.CountAppSubRolesAsync(searchTerm, token),
				tags: [AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	public Task<AuthUserAppRole?> GetAppSubRoleAsync(int appSubRoleId) =>
		_authRepository.GetAppSubRoleAsync(appSubRoleId);

	// Deliberately uncached, unlike the list reads above. This one decides whether a write is
	// allowed, and a cached "no duplicate" would stay true for the life of the entry - long
	// enough for two operators to each be told the assignment is free and both create it.
	public Task<bool> AppSubRoleExistsAsync(
		Guid userId,
		int appId,
		int subMenuId,
		int? excludeAppRoleId,
		CancellationToken cancellationToken) =>
		_authRepository.AppSubRoleExistsAsync(userId, appId, subMenuId, excludeAppRoleId, cancellationToken);
	
	public async Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
		{
			var result = await _authRepository.AddAppSubRoleAsync(appSubRole);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return result;
		}
	
	public async Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole)
		{
			var result = await _authRepository.DeleteAppSubRoleAsync(appSubRole);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return result;
		}
	
	public async Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole)
		{
			var updated = await _authRepository.EditAppSubRoleAsync(appSubRole);
	
			if (updated != null)
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return updated!;
		}
}
