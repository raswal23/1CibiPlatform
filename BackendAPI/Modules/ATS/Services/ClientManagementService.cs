namespace ATS.Services;

public class ClientManagementService : IClientManagementService
{
	private readonly IATSRepository _atsRepository;
	private readonly ILogger<ClientManagementService> _logger;

	public ClientManagementService(IATSRepository atsRepository,
					   ILogger<ClientManagementService> logger)
	{
		_atsRepository = atsRepository;
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
			_atsRepository.GetClientsAsync(paginationRequest, cancellationToken) :
			_atsRepository.SearchClientsAsync(paginationRequest, cancellationToken);
	}

	public async Task<bool> AddClientAsync(
		IReadOnlyCollection<AddClientDTO> clientDTOs,
		CancellationToken cancellationToken)
	{
		var client = clientDTOs.First();
		var logContext = new
		{
			Action = "AddClient",
			Step = "CreatingClient",
			ClientName = client.ClientName,
			PackageCount = clientDTOs.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding client: {@Context}", logContext);

		var isAdded = await _atsRepository.AddClientAsync(clientDTOs, cancellationToken);
		return isAdded;
	}

	public async Task<IReadOnlyList<ClientDetailsDTO>> EditClientAsync(
		IReadOnlyCollection<EditClientDTO> clientDTOs,
		CancellationToken cancellationToken)
	{
		var client = clientDTOs.First();
		var logContext = new
		{
			Action = "EditClient",
			Step = "SynchronizingClientPackages",
			ClientId = client.ClientId,
			PackageCount = clientDTOs.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Synchronizing client package assignments: {@Context}", logContext);

		var updatedClients = await _atsRepository.EditClientAsync(clientDTOs, cancellationToken);
		return updatedClients.Adapt<IReadOnlyList<ClientDetailsDTO>>();
	}
}
