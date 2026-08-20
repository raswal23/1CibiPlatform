namespace ATS.Data.Cache.Administration;

public sealed class ClientCacheRepository : IClientRepository
{
	private readonly IClientRepository _repository;
	private readonly HybridCache _cache;

	public ClientCacheRepository(IClientRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<ClientLookupDTO>> GetClientPageKeysAsync(string? searchTerm, string? afterClientName, int? afterClientId, int take, CancellationToken cancellationToken)
	{
		if (afterClientName is not null)
			return _repository.GetClientPageKeysAsync(searchTerm, afterClientName, afterClientId, take, cancellationToken);

		var key = $"client_v2_first_take_{take}_search_{searchTerm}";
		return _cache.GetOrCreateAsync<List<ClientLookupDTO>>(
			key, async token => await _repository.GetClientPageKeysAsync(searchTerm, null, null, take, token),
			tags: [CacheTags.Client], cancellationToken: cancellationToken).AsTask();
	}

	public Task<List<ClientDetailsDTO>> GetClientsByIdsAsync(IReadOnlyCollection<int> clientIds, string? searchTerm, CancellationToken cancellationToken) =>
		_repository.GetClientsByIdsAsync(clientIds, searchTerm, cancellationToken);

	public Task<long> CountClientsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_cache.GetOrCreateAsync<long>(
			$"client_v2_count_search_{searchTerm}", async token => await _repository.CountClientsAsync(searchTerm, token),
			tags: [CacheTags.Client], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var result = await _repository.AddClientAsync(clientDTOs, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.Client, cancellationToken);
		return result;
	}

	public Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken) =>
		_repository.GetClientAsync(clientId, cancellationToken);

	public Task<bool> ClientNameExistsAsync(string clientName, int? excludeClientId, CancellationToken cancellationToken) =>
		_repository.ClientNameExistsAsync(clientName, excludeClientId, cancellationToken);

	public Task<int> CountActivePackagesAsync(IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken) =>
		_repository.CountActivePackagesAsync(packageIds, cancellationToken);

	public async Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var result = await _repository.EditClientAsync(clientDTOs, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.Client, cancellationToken);
		await _cache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}
}
