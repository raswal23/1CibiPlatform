namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<RolesDTO>> GetRolesPageAsync(string? searchTerm, int? afterRoleId, int take, CancellationToken cancellationToken)
		{
			if (afterRoleId.HasValue)
				return await _authRepository.GetRolesPageAsync(searchTerm, afterRoleId, take, cancellationToken);

			var cacheKey = $"roles_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<RolesDTO>>(
				cacheKey,
				async token => await _authRepository.GetRolesPageAsync(searchTerm, null, take, token),
				tags: [RolesTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"roles_count_search_{searchTerm}",
				async token => await _authRepository.CountRolesAsync(searchTerm, token),
				tags: [RolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<bool> AddRoleAsync(AddRoleDTO role)
		{
			var result = await _authRepository.AddRoleAsync(role);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(RolesTag);
	
			return result;
		}
	
	public async Task<bool> DeleteRoleAsync(AuthRole role)
		{
			var result = await _authRepository.DeleteRoleAsync(role);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(RolesTag);
	
			return result;
		}
	
	public async Task<AuthRole> GetRoleAsync(int roleId)
		{
			return await _authRepository.GetRoleAsync(roleId);
		}
	
	public async Task<AuthRole> EditRoleAsync(AuthRole role)
		{
			var updated = await _authRepository.EditRoleAsync(role);
	
			if (updated != null)
				await _hybridCache.RemoveByTagAsync(RolesTag);
			    await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return updated!;
		}
}
