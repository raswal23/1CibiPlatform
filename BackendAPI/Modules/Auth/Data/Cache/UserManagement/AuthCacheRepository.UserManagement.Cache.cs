namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PaginatedResult<UsersDTO>> GetUserAsync(
			PaginationRequest paginationRequest,
			CancellationToken cancellationToken)
		{
			var cacheKey = $"users_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetUserAsync(req, token),
				null,
				tags: [UsersTag],
				cancellationToken);
		}
	
	public async Task<PaginatedResult<UsersDTO>> SearchUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"users_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchUserAsync(req, token),
				null,
				null,
				cancellationToken);
		}
	
	public async Task<PaginatedResult<UsersDTO>> GetUnapprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"unapprovedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.GetUnapprovedUserAsync(req, token),
				null,
				tags: [UnApprovedUsersTag],
				cancellationToken);
		}
	
	public async Task<PaginatedResult<UsersDTO>> SearchUnApprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var cacheKey = $"unapprovedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";
	
			return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
				cacheKey,
				paginationRequest,
				async (req, token) => await _authRepository.SearchUnApprovedUserAsync(req, token),
				null,
				null,
				cancellationToken);
		}
	
	public async Task<Authusers> GetRawUserAsync(Guid id)
		{
			return await _authRepository.GetRawUserAsync(id);
		}
	
	public async Task<Authusers> EditUserAsync(Authusers user)
		{
			var updated = await _authRepository.EditUserAsync(user);
	
			if (updated != null)
				await _hybridCache.RemoveByTagAsync(UsersTag);
			await _hybridCache.RemoveByTagAsync(UnApprovedUsersTag);
	
			return updated!;
		}
	
	public async Task<Authusers> GetUserAsync(string email)
		{
			return await _authRepository.GetUserAsync(email);
		}
}
