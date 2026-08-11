namespace ATS.Data.Repository.Administration.UserClient;

public interface IUserClientRepository
{
	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
	Task<PaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken);
}
