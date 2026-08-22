namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<ClientLookupDTO>> GetClientPageKeysAsync(string? searchTerm, string? afterClientName, int? afterClientId, int take, CancellationToken cancellationToken)
	{
		if (afterClientName is not null)
			return _atsRepository.GetClientPageKeysAsync(searchTerm, afterClientName, afterClientId, take, cancellationToken);

		var key = $"client_v2_first_take_{take}_search_{searchTerm}";
		return _hybridCache.GetOrCreateAsync<List<ClientLookupDTO>>(
			key, async token => await _atsRepository.GetClientPageKeysAsync(searchTerm, null, null, take, token),
			tags: [CacheTags.Client], cancellationToken: cancellationToken).AsTask();
	}

	public Task<List<ClientDetailsDTO>> GetClientsByIdsAsync(IReadOnlyCollection<int> clientIds, string? searchTerm, CancellationToken cancellationToken) =>
		_atsRepository.GetClientsByIdsAsync(clientIds, searchTerm, cancellationToken);

	public Task<long> CountClientsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<long>(
			$"client_v2_count_search_{searchTerm}", async token => await _atsRepository.CountClientsAsync(searchTerm, token),
			tags: [CacheTags.Client], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AddClientAsync(clientDTOs, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Client, cancellationToken);
		return result;
	}

	public Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken) =>
		_atsRepository.GetClientAsync(clientId, cancellationToken);

	public Task<bool> ClientNameExistsAsync(string clientName, int? excludeClientId, CancellationToken cancellationToken) =>
		_atsRepository.ClientNameExistsAsync(clientName, excludeClientId, cancellationToken);

	public Task<int> CountActivePackagesAsync(IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken) =>
		_atsRepository.CountActivePackagesAsync(packageIds, cancellationToken);

	public async Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.EditClientAsync(clientDTOs, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Client, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}
}
