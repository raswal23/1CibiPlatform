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

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<ModuleDetailsDTO>> GetModulesPageAsync(string? searchTerm, string? afterModuleName, int take, CancellationToken cancellationToken)
	{
		if (afterModuleName is not null)
			return _repository.GetModulesPageAsync(searchTerm, afterModuleName, take, cancellationToken);

		var key = $"module_first_take_{take}_search_{searchTerm}";
		return _cache.GetOrCreateAsync<List<ModuleDetailsDTO>>(
			key, async token => await _repository.GetModulesPageAsync(searchTerm, null, take, token),
			tags: [CacheTags.Module], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountModulesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_cache.GetOrCreateAsync<long>(
			$"module_count_search_{searchTerm}", async token => await _repository.CountModulesAsync(searchTerm, token),
			tags: [CacheTags.Module], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var result = await _repository.AddModuleAsync(moduleDTO);
		if (result)
			await _cache.RemoveByTagAsync(CacheTags.Module);
		return result;
	}

	public Task<ModuleDetails?> GetModuleAsync(int moduleId) => _repository.GetModuleAsync(moduleId);

	public Task<bool> ModuleNameExistsAsync(string moduleName) => _repository.ModuleNameExistsAsync(moduleName);

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails)
	{
		var result = await _repository.EditModuleAsync(moduleDetails);
		await _cache.RemoveByTagAsync(CacheTags.Module);
		return result;
	}
}
