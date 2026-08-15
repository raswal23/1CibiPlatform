namespace ATS.Data.Repository.Administration.Clients;

public interface IClientRepository
{
	Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<ClientDetailsDTO>> SearchClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken);
	Task<bool> ClientNameExistsAsync(string clientName, int? excludeClientId, CancellationToken cancellationToken);
	Task<int> CountActivePackagesAsync(IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken);
	Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken);
	Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken);
}
