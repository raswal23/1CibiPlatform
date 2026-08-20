namespace Auth.Shared.Contracts;

public interface IAuthQueries
{
	Task<IReadOnlyList<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
		CancellationToken cancellationToken);

	Task<KeysetPaginatedResult<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(
		Guid userId,
		CancellationToken cancellationToken);

	Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(
		IReadOnlyCollection<string> emails,
		CancellationToken cancellationToken);
}
