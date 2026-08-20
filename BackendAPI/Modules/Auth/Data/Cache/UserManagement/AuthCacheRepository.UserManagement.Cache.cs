namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor): it is by far
	// the most-requested page, while cursor pages are high-cardinality and would
	// balloon the cache with near-zero hit rate.
	public async Task<List<UsersDTO>> GetUsersPageAsync(string? searchTerm, Guid? afterId, int take, CancellationToken cancellationToken)
		{
			if (afterId.HasValue)
				return await _authRepository.GetUsersPageAsync(searchTerm, afterId, take, cancellationToken);

			var cacheKey = $"users_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<UsersDTO>>(
				cacheKey,
				async token => await _authRepository.GetUsersPageAsync(searchTerm, null, take, token),
				tags: [UsersTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountUsersAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"users_count_search_{searchTerm}",
				async token => await _authRepository.CountUsersAsync(searchTerm, token),
				tags: [UsersTag],
				cancellationToken: cancellationToken);
		}

	public async Task<List<UsersDTO>> GetUnapprovedUsersPageAsync(string? searchTerm, Guid? afterId, int take, CancellationToken cancellationToken)
		{
			if (afterId.HasValue)
				return await _authRepository.GetUnapprovedUsersPageAsync(searchTerm, afterId, take, cancellationToken);

			var cacheKey = $"unapprovedusers_first_take_{take}_search_{searchTerm}";

			return await _hybridCache.GetOrCreateAsync<List<UsersDTO>>(
				cacheKey,
				async token => await _authRepository.GetUnapprovedUsersPageAsync(searchTerm, null, take, token),
				tags: [UnApprovedUsersTag],
				cancellationToken: cancellationToken);
		}

	public async Task<long> CountUnapprovedUsersAsync(string? searchTerm, CancellationToken cancellationToken)
		{
			return await _hybridCache.GetOrCreateAsync<long>(
				$"unapprovedusers_count_search_{searchTerm}",
				async token => await _authRepository.CountUnapprovedUsersAsync(searchTerm, token),
				tags: [UnApprovedUsersTag],
				cancellationToken: cancellationToken);
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
