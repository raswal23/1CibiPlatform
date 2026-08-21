namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<ModuleDetailsDTO>> GetModulesPageAsync(string? searchTerm, string? afterModuleName, int take, CancellationToken cancellationToken)
	{
		if (afterModuleName is not null)
			return _atsRepository.GetModulesPageAsync(searchTerm, afterModuleName, take, cancellationToken);

		var key = $"module_first_take_{take}_search_{searchTerm}";
		return _hybridCache.GetOrCreateAsync<List<ModuleDetailsDTO>>(
			key, async token => await _atsRepository.GetModulesPageAsync(searchTerm, null, take, token),
			tags: [CacheTags.Module], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountModulesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<long>(
			$"module_count_search_{searchTerm}", async token => await _atsRepository.CountModulesAsync(searchTerm, token),
			tags: [CacheTags.Module], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var result = await _atsRepository.AddModuleAsync(moduleDTO);
		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Module);
		return result;
	}

	public Task<ModuleDetails?> GetModuleAsync(int moduleId) => _atsRepository.GetModuleAsync(moduleId);

	public Task<bool> ModuleNameExistsAsync(string moduleName) => _atsRepository.ModuleNameExistsAsync(moduleName);

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails)
	{
		var result = await _atsRepository.EditModuleAsync(moduleDetails);
		await _hybridCache.RemoveByTagAsync(CacheTags.Module);
		return result;
	}
}
