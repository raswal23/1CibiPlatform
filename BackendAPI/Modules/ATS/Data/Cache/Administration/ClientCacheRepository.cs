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

	public Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"client_v2_page_{request.PageIndex}_size_{request.PageSize}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ClientDetailsDTO>>(
			key, request, async (value, token) => await _repository.GetClientsAsync(value, token), null,
			tags: [CacheTags.Client], cancellationToken: cancellationToken).AsTask();
	}

	public Task<PaginatedResult<ClientDetailsDTO>> SearchClientsAsync(PaginationRequest request, CancellationToken cancellationToken)
	{
		var key = $"client_v2_page_{request.PageIndex}_size_{request.PageSize}_search_{request.SearchTerm}";
		return _cache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ClientDetailsDTO>>(
			key, request, async (value, token) => await _repository.SearchClientsAsync(value, token), null,
			tags: [CacheTags.Client], cancellationToken: cancellationToken).AsTask();
	}

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
