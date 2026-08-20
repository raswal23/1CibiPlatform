namespace Auth.Data.Repository;

public interface IUserDirectoryRepository
{
	Task<List<ATSUserLookupDTO>> GetATSAssignedUsersAsync(CancellationToken cancellationToken);
	// Rows come back with the name parts set but UserName empty — AuthQueries joins it.
	Task<List<ATSUserLookupDTO>> GetATSAssignedUsersPageAsync(string? searchTerm, string? afterLastName, string? afterFirstName, Guid? afterId, int take, CancellationToken cancellationToken);
	Task<long> CountATSAssignedUsersAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken);
}
