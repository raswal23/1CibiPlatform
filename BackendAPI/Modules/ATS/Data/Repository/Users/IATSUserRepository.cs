namespace ATS.Data.Repository;

public interface IATSUserRepository
{
	Task<List<UserPageKeyDTO>> GetUserPageKeysAsync(string? searchTerm, int? clientId, string? afterUserName, string? afterUserEmail, Guid? afterUserId, int take, CancellationToken cancellationToken);
	Task<List<UserDetailsDTO>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, string? searchTerm, int? clientId, CancellationToken cancellationToken);
	Task<long> CountUsersAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken);
	Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken);
	Task<bool> UserExistsAsync(Guid userId, string email, CancellationToken cancellationToken);
	Task<bool> UserEmailExistsAsync(Guid userId, string email, CancellationToken cancellationToken);
	Task<bool> RoleIsActiveAsync(int roleId, CancellationToken cancellationToken);
	Task<int> CountActiveModulesAsync(IReadOnlyCollection<int> moduleIds, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<int>> GetActiveUserRoleIdsAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken);
}
