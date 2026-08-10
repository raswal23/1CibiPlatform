namespace ATS.Data.Cache.Administration;

public sealed class ATSUserCacheRepository : IATSUserRepository
{
	private const string UserTag = "user";
	private const string UserClientTag = "userclient";
	private const string ModuleTag = "module";
	private readonly IATSUserRepository _repository;
	private readonly HybridCache _cache;

	public ATSUserCacheRepository(IATSUserRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	public Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(CancellationToken cancellationToken) =>
		GetAssignmentsAsync(cancellationToken);

	private async Task<IReadOnlyList<UserClientDetailsDTO>> GetAssignmentsAsync(CancellationToken cancellationToken) =>
		await _cache.GetOrCreateAsync<List<UserClientDetailsDTO>>(
			"user_client_assignments",
			async token => (await _repository.GetUserClientAssignmentsAsync(token)).ToList(),
			tags: [UserClientTag], cancellationToken: cancellationToken);

	public Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		IReadOnlyCollection<Guid> userIds,
		CancellationToken cancellationToken) =>
		_repository.GetUserClientAssignmentsAsync(userIds, cancellationToken);

	public Task<PaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken) =>
		_repository.GetAssignableClientsAsync(paginationRequest, cancellationToken);

	public Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken) =>
		_cache.GetOrCreateAsync<UserClientDetails?>(
			$"user_client_assignment_{userId}",
			async token => await _repository.GetUserClientAssignmentAsync(userId, token),
			tags: [UserClientTag], cancellationToken: cancellationToken).AsTask();

	public async Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken)
	{
		var result = await _repository.AssignUserClientAsync(assignment, cancellationToken);
		await _cache.RemoveByTagAsync(UserClientTag, cancellationToken);
		await _cache.RemoveByTagAsync(UserTag, cancellationToken);
		return result;
	}

	public Task<PaginatedResult<UserDetailsDTO>> GetUsersAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"user_page_{request.PageIndex}_size_{request.PageSize}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UserDetailsDTO>>(
			key, request, async (value, token) => await _repository.GetUsersAsync(value, token), null,
			tags: [UserTag], cancellationToken: cancellationToken).AsTask();
	}

	public Task<PaginatedResult<UserDetailsDTO>> SearchUsersAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"user_page_{request.PageIndex}_size_{request.PageSize}_search_{request.SearchTerm}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UserDetailsDTO>>(
			key, request, async (value, token) => await _repository.SearchUsersAsync(value, token), null,
			tags: [UserTag], cancellationToken: cancellationToken).AsTask();
	}

	public async Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var result = await _repository.AddUserAsync(userDTOs, cancellationToken);
		if (result)
		{
			await _cache.RemoveByTagAsync(UserTag, cancellationToken);
			await _cache.RemoveByTagAsync(UserClientTag, cancellationToken);
		}
		return result;
	}

	public Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
		_repository.GetUserAsync(userId, cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _cache.GetOrCreateAsync<List<int>>(
			$"user_active_modules_{userId}",
			async token => (await _repository.GetActiveUserModuleIdsAsync(userId, token)).ToList(),
			tags: [UserTag, ModuleTag], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var result = await _repository.EditUserAsync(userDTOs, cancellationToken);
		await _cache.RemoveByTagAsync(UserTag, cancellationToken);
		return result;
	}
}
