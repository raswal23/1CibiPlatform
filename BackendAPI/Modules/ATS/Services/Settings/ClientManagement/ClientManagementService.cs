namespace ATS.Services.Settings.ClientManagement;

public class ClientManagementService : IClientManagementService
{
	private readonly IClientRepository _clientRepository;
	private readonly ILogger<ClientManagementService> _logger;

	public ClientManagementService(IClientRepository clientRepository,
					   ILogger<ClientManagementService> logger)
	{
		_clientRepository = clientRepository;
		_logger = logger;
	}

	public async Task<KeysetPaginatedResult<ClientDetailsDTO>> GetClientsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetClients",
			Step = "FetchingClients",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching clients with pagination: {@Context}", logContext);

		// Keyset over the grouped (ClientName, ClientId) keys; the page items are
		// re-fetched by id so each logical client expands to one row per package. An
		// undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		int? afterClientId = int.TryParse(fields?[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clientId) ? clientId : null;
		var afterClientName = afterClientId.HasValue ? fields![0] : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var keys = await _clientRepository.GetClientPageKeysAsync(
			paginationRequest.SearchTerm, afterClientName, afterClientName is null ? null : afterClientId,
			pageSize + 1, cancellationToken);
		var (pageKeys, hasMore) = KeysetPage.Trim(keys, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(pageKeys[^1].ClientName,
				pageKeys[^1].ClientId.ToString(CultureInfo.InvariantCulture))
			: null;
		long? totalCount = afterClientName is null
			? await _clientRepository.CountClientsAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		var items = pageKeys.Count == 0
			? []
			: await _clientRepository.GetClientsByIdsAsync(
				pageKeys.Select(key => key.ClientId).ToList(), paginationRequest.SearchTerm, cancellationToken);

		return new KeysetPaginatedResult<ClientDetailsDTO>(items, nextCursor, totalCount);
	}

	public async Task<bool> AddClientAsync(
		IReadOnlyCollection<AddClientDTO> clientDTOs,
		CancellationToken cancellationToken)
	{
		if (clientDTOs.Count == 0)
			throw new BadRequestException("At least one package must be selected.");

		var client = clientDTOs.First();
		var clientName = client.ClientName.Trim();
		if (await _clientRepository.ClientNameExistsAsync(clientName, null, cancellationToken))
			throw new BadRequestException($"Client '{clientName}' already exists.");

		var packageIds = clientDTOs.Select(item => item.PackageId).Distinct().ToArray();
		if (await _clientRepository.CountActivePackagesAsync(packageIds, cancellationToken) != packageIds.Length)
			throw new BadRequestException("One or more selected packages do not exist or are inactive.");

		var logContext = new
		{
			Action = "AddClient",
			Step = "CreatingClient",
			ClientName = client.ClientName,
			PackageCount = clientDTOs.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding client: {@Context}", logContext);

		var isAdded = await _clientRepository.AddClientAsync(clientDTOs, cancellationToken);
		return isAdded;
	}

	public async Task<IReadOnlyList<ClientDetailsDTO>> EditClientAsync(
		IReadOnlyCollection<EditClientDTO> clientDTOs,
		CancellationToken cancellationToken)
	{
		if (clientDTOs.Count == 0)
			throw new BadRequestException("At least one package must be selected.");

		var client = clientDTOs.First();
		var existing = await _clientRepository.GetClientAsync(client.ClientId, cancellationToken);
		if (existing.Count == 0)
			throw new NotFoundException($"Client with ID {client.ClientId} was not found.");

		var clientName = client.ClientName.Trim();
		if (await _clientRepository.ClientNameExistsAsync(clientName, client.ClientId, cancellationToken))
			throw new BadRequestException($"Client '{clientName}' already exists.");

		var newPackageIds = clientDTOs.Select(item => item.PackageId).Distinct()
			.Except(existing.Select(item => item.PackageId)).ToArray();
		if (newPackageIds.Length > 0 && await _clientRepository.CountActivePackagesAsync(newPackageIds, cancellationToken) != newPackageIds.Length)
			throw new BadRequestException("One or more newly selected packages do not exist or are inactive.");

		var logContext = new
		{
			Action = "EditClient",
			Step = "SynchronizingClientPackages",
			ClientId = client.ClientId,
			PackageCount = clientDTOs.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Synchronizing client package assignments: {@Context}", logContext);

		var updatedClients = await _clientRepository.EditClientAsync(clientDTOs, cancellationToken);
		return updatedClients.Adapt<IReadOnlyList<ClientDetailsDTO>>();
	}
}
