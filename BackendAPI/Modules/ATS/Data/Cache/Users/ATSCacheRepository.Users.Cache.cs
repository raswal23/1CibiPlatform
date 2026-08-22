namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<UserPageKeyDTO>> GetUserPageKeysAsync(string? searchTerm, int? clientId, string? afterUserName, string? afterUserEmail, Guid? afterUserId, int take, CancellationToken cancellationToken)
	{
		if (afterUserName is not null)
			return _atsRepository.GetUserPageKeysAsync(searchTerm, clientId, afterUserName, afterUserEmail, afterUserId, take, cancellationToken);

		var scope = clientId.HasValue ? $"client_{clientId.Value}" : "all";
		var key = $"user_scope_{scope}_first_take_{take}_search_{searchTerm}";
		return _hybridCache.GetOrCreateAsync<List<UserPageKeyDTO>>(
			key, async token => await _atsRepository.GetUserPageKeysAsync(searchTerm, clientId, null, null, null, take, token),
			tags: [CacheTags.User], cancellationToken: cancellationToken).AsTask();
	}

	public Task<List<UserDetailsDTO>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		_atsRepository.GetUsersByIdsAsync(userIds, searchTerm, clientId, cancellationToken);

	public Task<long> CountUsersAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken)
	{
		var scope = clientId.HasValue ? $"client_{clientId.Value}" : "all";
		return _hybridCache.GetOrCreateAsync<long>(
			$"user_scope_{scope}_count_search_{searchTerm}",
			async token => await _atsRepository.CountUsersAsync(searchTerm, clientId, token),
			tags: [CacheTags.User], cancellationToken: cancellationToken).AsTask();
	}

	public async Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AddUserAsync(userDTOs, cancellationToken);
		if (result)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.User, cancellationToken);
			await _hybridCache.RemoveByTagAsync(CacheTags.UserClient, cancellationToken);
		}
		return result;
	}

	public Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
		_atsRepository.GetUserAsync(userId, cancellationToken);

	public Task<bool> UserExistsAsync(Guid userId, string email, CancellationToken cancellationToken) =>
		_atsRepository.UserExistsAsync(userId, email, cancellationToken);

	public Task<bool> UserEmailExistsAsync(Guid userId, string email, CancellationToken cancellationToken) =>
		_atsRepository.UserEmailExistsAsync(userId, email, cancellationToken);

	public Task<bool> RoleIsActiveAsync(int roleId, CancellationToken cancellationToken) =>
		_atsRepository.RoleIsActiveAsync(roleId, cancellationToken);

	public Task<int> CountActiveModulesAsync(IReadOnlyCollection<int> moduleIds, CancellationToken cancellationToken) =>
		_atsRepository.CountActiveModulesAsync(moduleIds, cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _hybridCache.GetOrCreateAsync<List<int>>(
			$"user_active_roles_{userId}",
			async token => (await _atsRepository.GetActiveUserRoleIdsAsync(userId, token)).ToList(),
			tags: [CacheTags.User, CacheTags.Role], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _hybridCache.GetOrCreateAsync<List<int>>(
			$"user_active_modules_{userId}",
			async token => (await _atsRepository.GetActiveUserModuleIdsAsync(userId, token)).ToList(),
			tags: [CacheTags.User, CacheTags.Module], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.EditUserAsync(userDTOs, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.User, cancellationToken);
		return result;
	}
}
