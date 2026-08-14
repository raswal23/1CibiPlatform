namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PaginatedResult<RolesDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"roles_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RolesDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetRolesAsync(req, token),
				tags: [RolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<PaginatedResult<RolesDTO>> SearchRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"roles_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RolesDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchRoleAsync(req, token),
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
