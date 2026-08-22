namespace ATS.Data.Repository;

public interface IUserClientRepository
{
	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(int clientId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
	Task<List<ClientLookupDTO>> GetAssignableClientsPageAsync(string? searchTerm, string? afterClientName, int? afterClientId, int take, CancellationToken cancellationToken);
	Task<long> CountAssignableClientsAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken);
	Task<bool> ClientIsActiveAsync(int clientId, CancellationToken cancellationToken);
	Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken);
}
