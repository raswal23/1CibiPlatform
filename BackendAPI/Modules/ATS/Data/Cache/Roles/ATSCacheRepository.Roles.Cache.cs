namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<RoleDetailsDTO>> GetRolesPageAsync(string? searchTerm, string? afterRoleName, int take, CancellationToken cancellationToken)
	{
		if (afterRoleName is not null)
			return _atsRepository.GetRolesPageAsync(searchTerm, afterRoleName, take, cancellationToken);

		var key = $"role_first_take_{take}_search_{searchTerm}";
		return _hybridCache.GetOrCreateAsync<List<RoleDetailsDTO>>(
			key, async token => await _atsRepository.GetRolesPageAsync(searchTerm, null, take, token),
			tags: [CacheTags.Role], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<long>(
			$"role_count_search_{searchTerm}", async token => await _atsRepository.CountRolesAsync(searchTerm, token),
			tags: [CacheTags.Role], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddRoleAsync(AddRoleDTO roleDTO)
	{
		var result = await _atsRepository.AddRoleAsync(roleDTO);
		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Role);
		return result;
	}

	public Task<RoleDetails?> GetRoleAsync(int roleId) => _atsRepository.GetRoleAsync(roleId);

	public async Task<RoleDetails> EditRoleAsync(RoleDetails roleDetails)
	{
		var result = await _atsRepository.EditRoleAsync(roleDetails);
		await _hybridCache.RemoveByTagAsync(CacheTags.Role);
		await _hybridCache.RemoveByTagAsync(CacheTags.User);
		return result;
	}
}
