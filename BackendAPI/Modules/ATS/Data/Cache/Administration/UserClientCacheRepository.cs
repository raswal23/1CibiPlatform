namespace ATS.Data.Cache.Administration;

public sealed class UserClientCacheRepository : IUserClientRepository
{
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
			tags: [CacheTags.UserClient], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		int clientId,
		CancellationToken cancellationToken) =>
		await _cache.GetOrCreateAsync<List<UserClientDetailsDTO>>(
			$"user_client_assignments_client_{clientId}",
			async token => (await _repository.GetUserClientAssignmentsAsync(clientId, token)).ToList(),
			tags: [CacheTags.UserClient], cancellationToken: cancellationToken);

	public Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		IReadOnlyCollection<Guid> userIds,
		CancellationToken cancellationToken) =>
		_repository.GetUserClientAssignmentsAsync(userIds, cancellationToken);

	public Task<List<ClientLookupDTO>> GetAssignableClientsPageAsync(
		string? searchTerm, string? afterClientName, int? afterClientId, int take,
		CancellationToken cancellationToken) =>
		_repository.GetAssignableClientsPageAsync(searchTerm, afterClientName, afterClientId, take, cancellationToken);

	public Task<long> CountAssignableClientsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_repository.CountAssignableClientsAsync(searchTerm, cancellationToken);

	public Task<bool> ClientIsActiveAsync(int clientId, CancellationToken cancellationToken) =>
		_repository.ClientIsActiveAsync(clientId, cancellationToken);

	public Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken) =>
		_cache.GetOrCreateAsync<UserClientDetails?>(
			$"user_client_assignment_{userId}",
			async token => await _repository.GetUserClientAssignmentAsync(userId, token),
			tags: [CacheTags.UserClient], cancellationToken: cancellationToken).AsTask();

	public async Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken)
	{
		var result = await _repository.AssignUserClientAsync(assignment, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.UserClient, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.User, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}
}
