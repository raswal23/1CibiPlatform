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

	public Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"role_page_{request.PageIndex}_size_{request.PageSize}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RoleDetailsDTO>>(
			key, request, async (value, token) => await _repository.GetRolesAsync(value, token), null,
			tags: [CacheTags.Role], cancellationToken: cancellationToken).AsTask();
	}

	public Task<PaginatedResult<RoleDetailsDTO>> SearchRolesAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"role_page_{request.PageIndex}_size_{request.PageSize}_search_{request.SearchTerm}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RoleDetailsDTO>>(
			key, request, async (value, token) => await _repository.SearchRolesAsync(value, token), null,
			tags: [CacheTags.Role], cancellationToken: cancellationToken).AsTask();
	}

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
