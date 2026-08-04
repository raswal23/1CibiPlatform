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

	public async Task<bool> AddClientAsync(AddClientDTO clientDTO)
	{
		var logContext = new
		{
			Action = "AddClient",
			Step = "CreatingClient",
			ClientName = clientDTO.ClientName,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding client: {@Context}", logContext);

		var isAdded = await _atsRepository.AddClientAsync(clientDTO);
		return isAdded;
	}

	public async Task<ClientDetailsDTO> EditClientAsync(EditClientDTO clientDTO)
	{
		var logContext = new
		{
			Action = "EditClient",
			Step = "FetchForUpdate",
			ClientId = clientDTO.ClientId,
			Timestamp = DateTime.UtcNow
		};

		var existingClient = await _atsRepository.GetClientAsync(clientDTO.ClientId);
		if (existingClient == null)
		{
			_logger.LogError("{ClientId} was not found during update operation: {@Context}", clientDTO.ClientId, logContext);
			throw new NotFoundException($"Client with ID {clientDTO.ClientId} was not found.");
		}

		existingClient.ClientName = clientDTO.ClientName!;
		existingClient.IsActive = clientDTO.IsActive;

		var client = await _atsRepository.EditClientAsync(existingClient);
		return client.Adapt<ClientDetailsDTO>();
	}
}
