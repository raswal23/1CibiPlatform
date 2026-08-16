namespace Auth.Data.Repository;

public interface IUserDirectoryRepository
{
	Task<List<ATSUserLookupDTO>> GetATSAssignedUsersAsync(CancellationToken cancellationToken);
	Task<PaginatedResult<ATSUserLookupDTO>> GetATSAssignedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken);
}
