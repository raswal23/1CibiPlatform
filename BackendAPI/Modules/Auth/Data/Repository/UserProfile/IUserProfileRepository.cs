namespace Auth.Data.Repository;

/// <summary>
/// Persistence for the authenticated user's own profile. Every operation is
/// scoped by user id; there is no "any user" overload by design.
/// </summary>
public interface IUserProfileRepository
{
	Task<Authusers?> GetProfileAsync(
		Guid userId,
		CancellationToken cancellationToken);

	Task<Authusers> UpdateProfileAsync(
		Authusers user,
		CancellationToken cancellationToken);
}
