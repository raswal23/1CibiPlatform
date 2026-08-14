namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"submenus_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<SubMenusDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetSubMenusAsync(req, token),
				tags: [SubMenusTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<PaginatedResult<SubMenusDTO>> SearchSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"submenus_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<SubMenusDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchSubMenusAsync(req, token),
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
