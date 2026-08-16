namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"applications_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ApplicationsDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetApplicationsAsync(req, token),
				tags: [ApplicationsTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<PaginatedResult<ApplicationsDTO>> SearchApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"applications_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ApplicationsDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchApplicationsAsync(req, token),
				tags: [ApplicationsTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<AuthApplication> GetApplicationAsync(int applicationId)
		{
			return await _authRepository.GetApplicationAsync(applicationId);
		}
	
	public async Task<bool> DeleteApplicationAsync(AuthApplication application)
		{
			var result = await _authRepository.DeleteApplicationAsync(application);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(ApplicationsTag);
	
			return result;
		}
	
	public async Task<bool> AddApplicationAsync(AddApplicationDTO application)
		{
			var result = await _authRepository.AddApplicationAsync(application);
	
			if (result)
				await _hybridCache.RemoveByTagAsync(ApplicationsTag);
	
			return result;
		}
	
	public async Task<AuthApplication> EditApplicationAsync(AuthApplication application)
		{
			var updated = await _authRepository.EditApplicationAsync(application);
	
			if (updated != null)
				await _hybridCache.RemoveByTagAsync(ApplicationsTag);
				await _hybridCache.RemoveByTagAsync(AppSubRolesTag);
	
			return updated!;
		}
}
