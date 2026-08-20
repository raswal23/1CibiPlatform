namespace ATS.Data.Cache.Administration;

public sealed class ATSUserCacheRepository : IATSUserRepository
{
	private readonly IATSUserRepository _repository;
	private readonly HybridCache _cache;

	public ATSUserCacheRepository(IATSUserRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<UserPageKeyDTO>> GetUserPageKeysAsync(string? searchTerm, int? clientId, string? afterUserName, string? afterUserEmail, Guid? afterUserId, int take, CancellationToken cancellationToken)
	{
		if (afterUserName is not null)
			return _repository.GetUserPageKeysAsync(searchTerm, clientId, afterUserName, afterUserEmail, afterUserId, take, cancellationToken);

		var scope = clientId.HasValue ? $"client_{clientId.Value}" : "all";
		var key = $"user_scope_{scope}_first_take_{take}_search_{searchTerm}";
		return _cache.GetOrCreateAsync<List<UserPageKeyDTO>>(
			key, async token => await _repository.GetUserPageKeysAsync(searchTerm, clientId, null, null, null, take, token),
			tags: [CacheTags.User], cancellationToken: cancellationToken).AsTask();
	}

	public Task<List<UserDetailsDTO>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		_repository.GetUsersByIdsAsync(userIds, searchTerm, clientId, cancellationToken);

	public Task<long> CountUsersAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken)
	{
		var scope = clientId.HasValue ? $"client_{clientId.Value}" : "all";
		return _cache.GetOrCreateAsync<long>(
			$"user_scope_{scope}_count_search_{searchTerm}",
			async token => await _repository.CountUsersAsync(searchTerm, clientId, token),
			tags: [CacheTags.User], cancellationToken: cancellationToken).AsTask();
	}

	public async Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var result = await _repository.AddUserAsync(userDTOs, cancellationToken);
		if (result)
		{
			await _cache.RemoveByTagAsync(CacheTags.User, cancellationToken);
			await _cache.RemoveByTagAsync(CacheTags.UserClient, cancellationToken);
		}
		return result;
	}

	public Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
		_repository.GetUserAsync(userId, cancellationToken);

	public Task<bool> UserExistsAsync(Guid userId, string email, CancellationToken cancellationToken) =>
		_repository.UserExistsAsync(userId, email, cancellationToken);

	public Task<bool> UserEmailExistsAsync(Guid userId, string email, CancellationToken cancellationToken) =>
		_repository.UserEmailExistsAsync(userId, email, cancellationToken);

	public Task<bool> RoleIsActiveAsync(int roleId, CancellationToken cancellationToken) =>
		_repository.RoleIsActiveAsync(roleId, cancellationToken);

	public Task<int> CountActiveModulesAsync(IReadOnlyCollection<int> moduleIds, CancellationToken cancellationToken) =>
		_repository.CountActiveModulesAsync(moduleIds, cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _cache.GetOrCreateAsync<List<int>>(
			$"user_active_roles_{userId}",
			async token => (await _repository.GetActiveUserRoleIdsAsync(userId, token)).ToList(),
			tags: [CacheTags.User, CacheTags.Role], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _cache.GetOrCreateAsync<List<int>>(
			$"user_active_modules_{userId}",
			async token => (await _repository.GetActiveUserModuleIdsAsync(userId, token)).ToList(),
			tags: [CacheTags.User, CacheTags.Module], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var result = await _repository.EditUserAsync(userDTOs, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.User, cancellationToken);
		return result;
	}
}
