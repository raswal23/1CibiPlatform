namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<List<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
			CancellationToken cancellationToken)
		{
			const string cacheKey = "ats_assigned_users";
	
			return await _hybridCache.GetOrCreateAsync<List<ATSUserLookupDTO>>(
				cacheKey,
				async token => await _authRepository.GetATSAssignedUsersAsync(token),
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<PaginatedResult<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
			PaginationRequest paginationRequest,
			CancellationToken cancellationToken)
		{
			var search = paginationRequest.SearchTerm?.Trim().ToLowerInvariant() ?? string.Empty;
			var cacheKey = $"ats_assigned_users_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{search}";
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ATSUserLookupDTO>>(
				cacheKey,
				paginationRequest,
				async (request, token) => await _authRepository.GetATSAssignedUsersAsync(request, token),
				null,
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
		}
	
	public async Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(
			Guid userId,
			CancellationToken cancellationToken) =>
			await _hybridCache.GetOrCreateAsync<ATSUserLookupDTO?>(
				$"ats_assigned_user_{userId}",
				async token => await _authRepository.GetATSAssignedUserAsync(userId, token),
				tags: [UsersTag, AppSubRolesTag],
				cancellationToken: cancellationToken);
	
	public Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(
			IReadOnlyCollection<string> emails,
			CancellationToken cancellationToken) =>
			_authRepository.GetUserIdsByEmailAsync(emails, cancellationToken);
}
