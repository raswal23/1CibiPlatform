namespace Auth.Shared.Contracts;

public interface IAuthQueries
{
	Task<IReadOnlyList<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
		CancellationToken cancellationToken);
}
