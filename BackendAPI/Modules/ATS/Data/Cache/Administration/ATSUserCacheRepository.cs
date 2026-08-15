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

	public Task<PaginatedResult<UserDetailsDTO>> GetUsersAsync(PaginationRequest request, int? clientId, CancellationToken cancellationToken)
	{
		var scope = clientId.HasValue ? $"client_{clientId.Value}" : "all";
		var key = $"user_scope_{scope}_page_{request.PageIndex}_size_{request.PageSize}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UserDetailsDTO>>(
			key, request, async (value, token) => await _repository.GetUsersAsync(value, clientId, token), null,
			tags: [CacheTags.User], cancellationToken: cancellationToken).AsTask();
	}

	public Task<PaginatedResult<UserDetailsDTO>> SearchUsersAsync(PaginationRequest request, int? clientId, CancellationToken cancellationToken)
	{
		var scope = clientId.HasValue ? $"client_{clientId.Value}" : "all";
		var key = $"user_scope_{scope}_page_{request.PageIndex}_size_{request.PageSize}_search_{request.SearchTerm}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UserDetailsDTO>>(
			key, request, async (value, token) => await _repository.SearchUsersAsync(value, clientId, token), null,
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
