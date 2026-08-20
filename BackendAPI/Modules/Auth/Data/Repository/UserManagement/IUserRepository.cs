namespace Auth.Data.Repository;

public interface IUserRepository
{
	Task<PaginatedResult<UsersDTO>> GetUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<UsersDTO>> GetUnapprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<Authusers> GetRawUserAsync(Guid id);
	Task<Authusers> GetUserAsync(string email);
	Task<PaginatedResult<UsersDTO>> SearchUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<UsersDTO>> SearchUnApprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<Authusers> EditUserAsync(Authusers user);
}
