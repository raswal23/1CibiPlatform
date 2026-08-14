namespace ATS.Data.Cache.Administration;

public sealed class ModuleCacheRepository : IModuleRepository
{
	private readonly IModuleRepository _repository;
	private readonly HybridCache _cache;

	public ModuleCacheRepository(IModuleRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	public Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"module_page_{request.PageIndex}_size_{request.PageSize}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ModuleDetailsDTO>>(
			key, request, async (value, token) => await _repository.GetModulesAsync(value, token), null,
			tags: [CacheTags.Module], cancellationToken: cancellationToken).AsTask();
	}

	public Task<PaginatedResult<ModuleDetailsDTO>> SearchModulesAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"module_page_{request.PageIndex}_size_{request.PageSize}_search_{request.SearchTerm}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ModuleDetailsDTO>>(
			key, request, async (value, token) => await _repository.SearchModulesAsync(value, token), null,
			tags: [CacheTags.Module], cancellationToken: cancellationToken).AsTask();
	}

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var result = await _repository.AddModuleAsync(moduleDTO);
		if (result)
			await _cache.RemoveByTagAsync(CacheTags.Module);
		return result;
	}

	public Task<ModuleDetails?> GetModuleAsync(int moduleId) => _repository.GetModuleAsync(moduleId);

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails)
	{
		var result = await _repository.EditModuleAsync(moduleDetails);
		await _cache.RemoveByTagAsync(CacheTags.Module);
		return result;
	}
}
