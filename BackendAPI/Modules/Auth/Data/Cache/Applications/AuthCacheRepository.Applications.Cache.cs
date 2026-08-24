namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<ApplicationsDTO>> GetApplicationsPageAsync(string? searchTerm, int? afterAppId, int take, CancellationToken cancellationToken)
		{
			if (afterAppId.HasValue)
				return await _authRepository.GetApplicationsPageAsync(searchTerm, afterAppId, take, cancellationToken);

			var cacheKey = $"applications_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<ApplicationsDTO>>(
				cacheKey,
				async token => await _authRepository.GetApplicationsPageAsync(searchTerm, null, take, token),
				tags: [ApplicationsTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountApplicationsAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"applications_count_search_{searchTerm}",
				async token => await _authRepository.CountApplicationsAsync(searchTerm, token),
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
