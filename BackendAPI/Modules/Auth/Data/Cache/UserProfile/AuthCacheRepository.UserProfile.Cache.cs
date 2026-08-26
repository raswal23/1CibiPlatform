namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	// The profile read is a single-row primary-key lookup for the caller only, so
	// caching it would add a per-user entry with no measurable win and a staleness
	// risk right after the user renames themselves. It stays a pass-through.
	public Task<Authusers?> GetProfileAsync(
		Guid userId,
		CancellationToken cancellationToken) =>
		_authRepository.GetProfileAsync(userId, cancellationToken);

	// A rename does change what the cached user lists and the ATS user directory
	// render, so the write still invalidates those tags.
	public async Task<Authusers> UpdateProfileAsync(
		Authusers user,
		CancellationToken cancellationToken)
	{
		var updated = await _authRepository.UpdateProfileAsync(user, cancellationToken);

		await _hybridCache.RemoveByTagAsync(UsersTag, cancellationToken);
		await _hybridCache.RemoveByTagAsync(UnApprovedUsersTag, cancellationToken);
		await _hybridCache.RemoveByTagAsync(AppSubRolesTag, cancellationToken);

		return updated;
	}
}
