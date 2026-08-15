namespace ATS.Services;

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

	public Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(
		PaginationRequest paginationRequest,
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

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_clientRepository.GetClientsAsync(paginationRequest, cancellationToken) :
			_clientRepository.SearchClientsAsync(paginationRequest, cancellationToken);
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
