namespace ATS.Data.Cache.Administration;

public sealed class RoleCacheRepository : IRoleRepository
{
	private readonly IRoleRepository _repository;
	private readonly HybridCache _cache;

	public RoleCacheRepository(IRoleRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<RoleDetailsDTO>> GetRolesPageAsync(string? searchTerm, string? afterRoleName, int take, CancellationToken cancellationToken)
	{
		if (afterRoleName is not null)
			return _repository.GetRolesPageAsync(searchTerm, afterRoleName, take, cancellationToken);

		var key = $"role_first_take_{take}_search_{searchTerm}";
		return _cache.GetOrCreateAsync<List<RoleDetailsDTO>>(
			key, async token => await _repository.GetRolesPageAsync(searchTerm, null, take, token),
			tags: [CacheTags.Role], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_cache.GetOrCreateAsync<long>(
			$"role_count_search_{searchTerm}", async token => await _repository.CountRolesAsync(searchTerm, token),
			tags: [CacheTags.Role], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddRoleAsync(AddRoleDTO roleDTO)
	{
		var result = await _repository.AddRoleAsync(roleDTO);
		if (result)
			await _cache.RemoveByTagAsync(CacheTags.Role);
		return result;
	}

	public Task<RoleDetails?> GetRoleAsync(int roleId) => _repository.GetRoleAsync(roleId);

	public async Task<RoleDetails> EditRoleAsync(RoleDetails roleDetails)
	{
		var result = await _repository.EditRoleAsync(roleDetails);
		await _cache.RemoveByTagAsync(CacheTags.Role);
		await _cache.RemoveByTagAsync(CacheTags.User);
		return result;
	}
}
