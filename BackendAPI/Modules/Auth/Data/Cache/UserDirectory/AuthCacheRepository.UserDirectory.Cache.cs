namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<List<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
			CancellationToken cancellationToken)
		{
			const string cacheKey = "ats_assigned_users";
	
			return await _hybridCache.GetOrCreateAsync<List<ATSUserLookupDTO>>(
				cacheKey,
				async token => await _authRepository.GetATSAssignedUsersAsync(token),
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<ATSUserLookupDTO>> GetATSAssignedUsersPageAsync(
			string? searchTerm,
			string? afterLastName,
			string? afterFirstName,
			Guid? afterId,
			int take,
			CancellationToken cancellationToken)
		{
			if (afterLastName is not null)
				return await _authRepository.GetATSAssignedUsersPageAsync(searchTerm, afterLastName, afterFirstName, afterId, take, cancellationToken);

			var search = searchTerm?.Trim().ToLowerInvariant() ?? string.Empty;
			var cacheKey = $"ats_assigned_users_first_take_{take}_search_{search}";
			return await _hybridCache.GetOrCreateAsync<List<ATSUserLookupDTO>>(
				cacheKey,
				async token => await _authRepository.GetATSAssignedUsersPageAsync(searchTerm, null, null, null, take, token),
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountATSAssignedUsersAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			var search = searchTerm?.Trim().ToLowerInvariant() ?? string.Empty;
			return await _hybridCache.GetOrCreateAsync<long>(
				$"ats_assigned_users_count_search_{search}",
				async token => await _authRepository.CountATSAssignedUsersAsync(searchTerm, token),
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(
			Guid userId,
			CancellationToken cancellationToken) =>
			await _hybridCache.GetOrCreateAsync<ATSUserLookupDTO?>(
				$"ats_assigned_user_{userId}",
				async token => await _authRepository.GetATSAssignedUserAsync(userId, token),
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
	
	public Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(
			IReadOnlyCollection<string> emails,
			CancellationToken cancellationToken) =>
			_authRepository.GetUserIdsByEmailAsync(emails, cancellationToken);
}
