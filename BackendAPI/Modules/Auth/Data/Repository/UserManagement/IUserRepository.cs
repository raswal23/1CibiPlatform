namespace Auth.Data.Repository;

public interface IUserRepository
{
	Task<List<UsersDTO>> GetUsersPageAsync(string? searchTerm, Guid? afterId, int take, CancellationToken cancellationToken);
	Task<long> CountUsersAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<List<UsersDTO>> GetUnapprovedUsersPageAsync(string? searchTerm, Guid? afterId, int take, CancellationToken cancellationToken);
	Task<long> CountUnapprovedUsersAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<Authusers> GetRawUserAsync(Guid id);

	/// <summary>Loads a user by id regardless of active state, for status editing.</summary>
	Task<Authusers> GetUserByIdAsync(Guid id);

	Task<Authusers> GetUserAsync(string email);
	Task<Authusers> EditUserAsync(Authusers user);
}
