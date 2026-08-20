namespace ATS.Data.Cache.Administration;

public sealed class UserClientCacheRepository : IUserClientRepository
{
	private const string UserTag = "user";
	private const string UserClientTag = "userclient";
	private readonly string PackageTag = "package";
	private readonly IUserClientRepository _repository;
	private readonly HybridCache _cache;

	public UserClientCacheRepository(IUserClientRepository repository, HybridCache cache)
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

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		int clientId,
		CancellationToken cancellationToken) =>
		await _cache.GetOrCreateAsync<List<UserClientDetailsDTO>>(
			$"user_client_assignments_client_{clientId}",
			async token => (await _repository.GetUserClientAssignmentsAsync(clientId, token)).ToList(),
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
		await _cache.RemoveByTagAsync(PackageTag, cancellationToken);
		return result;
	}
}
