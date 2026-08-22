namespace ATS.Data.Repository;

public interface IClientRepository
{
	Task<List<ClientLookupDTO>> GetClientPageKeysAsync(string? searchTerm, string? afterClientName, int? afterClientId, int take, CancellationToken cancellationToken);
	Task<List<ClientDetailsDTO>> GetClientsByIdsAsync(IReadOnlyCollection<int> clientIds, string? searchTerm, CancellationToken cancellationToken);
	Task<long> CountClientsAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken);
	Task<bool> ClientNameExistsAsync(string clientName, int? excludeClientId, CancellationToken cancellationToken);
	Task<int> CountActivePackagesAsync(IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken);
	Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken);
	Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken);
}
