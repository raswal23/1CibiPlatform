namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"appsubroles_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AppSubRolesDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetAppSubRolesAsync(req, token),
				tags: [AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<PaginatedResult<AppSubRolesDTO>> SearchAppSubRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"appsubroles_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AppSubRolesDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchAppSubRoleAsync(req, token),
				tags: [AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<AuthUserAppRole> GetAppSubRoleAsync(int appSubRoleId)
		{
			return await _authRepository.GetAppSubRoleAsync(appSubRoleId);
		}
	
	public async Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
		{
			var result = await _authRepository.AddAppSubRoleAsync(appSubRole);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return result;
		}
	
	public async Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole)
		{
			var result = await _authRepository.DeleteAppSubRoleAsync(appSubRole);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return result;
		}
	
	public async Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole)
		{
			var updated = await _authRepository.EditAppSubRoleAsync(appSubRole);
	
			if (updated != null)
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return updated!;
		}
}
