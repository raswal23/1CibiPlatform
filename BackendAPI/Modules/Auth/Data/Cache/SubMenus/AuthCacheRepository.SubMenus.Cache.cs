namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<SubMenusDTO>> GetSubMenusPageAsync(string? searchTerm, int? afterSubMenuId, int take, CancellationToken cancellationToken)
		{
			if (afterSubMenuId.HasValue)
				return await _authRepository.GetSubMenusPageAsync(searchTerm, afterSubMenuId, take, cancellationToken);

			var cacheKey = $"submenus_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<SubMenusDTO>>(
				cacheKey,
				async token => await _authRepository.GetSubMenusPageAsync(searchTerm, null, take, token),
				tags: [SubMenusTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountSubMenusAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"submenus_count_search_{searchTerm}",
				async token => await _authRepository.CountSubMenusAsync(searchTerm, token),
				tags: [SubMenusTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu)
		{
			var result = await _authRepository.AddSubMenuAsync(subMenu);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(SubMenusTag);
	
			return result;
		}
	
	public async Task<bool> DeleteSubMenuAsync(AuthSubMenu subMenu)
		{
			var result = await _authRepository.DeleteSubMenuAsync(subMenu);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(SubMenusTag);
	
			return result;
		}
	
	public async Task<AuthSubMenu> EditSubMenuAsync(AuthSubMenu subMenu)
		{
			var updated = await _authRepository.EditSubMenuAsync(subMenu);
	
			if (updated != null)
				await _hybridCache.RemoveByTagAsync(SubMenusTag);
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return updated!;
		}
	
	public async Task<AuthSubMenu> GetSubMenuAsync(int applicationId)
		{
			return await _authRepository.GetSubMenuAsync(applicationId);
		}
}
