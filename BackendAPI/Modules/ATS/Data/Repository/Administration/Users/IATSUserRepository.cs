namespace ATS.Data.Repository.Administration.Users;

public interface IATSUserRepository
{
	Task<PaginatedResult<UserDetailsDTO>> GetUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<UserDetailsDTO>> SearchUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken);
}
