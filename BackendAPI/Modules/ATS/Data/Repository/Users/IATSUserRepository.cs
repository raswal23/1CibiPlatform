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

	/// <summary>
	/// Everyone who should be told when something breaks system-wide, rather than when one
	/// order of theirs changes.
	/// </summary>
	/// <remarks>
	/// Selected by ROLE (Platform Manager and Admin), not by who holds the Email Accounts
	/// module. Module grants are per user in <c>UserDetails</c>, and module 16 is in no seeded
	/// role's grant list - so a module-based query returns nobody on a fresh database and the
	/// "every sender account is down" notification would go unsent precisely when it matters
	/// most. Roles 1 and 2 are the pair <c>AtsAccessScopeResolver</c> already treats as the
	/// elevated tier.
	/// </remarks>
	Task<IReadOnlyList<Guid>> GetAtsAdministratorUserIdsAsync(CancellationToken cancellationToken);
}
