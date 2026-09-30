namespace ATS.Services.AccessScope;

// The hard backend counterpart to the ATSLayout module check: the frontend redirects
// a disabled user to /access-denied, but their JWT stays valid until it expires, so
// endpoints must not trust the token alone. Applied per endpoint via
// RequireActiveAtsUser (AtsEndpointExtensions).
public sealed class AtsActiveUserGuard : IAtsActiveUserGuard
{
	private readonly ICurrentUser _currentUser;
	private readonly IATSUserRepository _userRepository;

	public AtsActiveUserGuard(ICurrentUser currentUser, IATSUserRepository userRepository)
	{
		_currentUser = currentUser;
		_userRepository = userRepository;
	}

	public async Task EnsureActiveAtsUserAsync(CancellationToken cancellationToken)
	{
		// Platform super admins administer ATS without an ats.UserDetails row
		// (same bypass as AtsAccessScopeResolver).
		if (_currentUser.IsPlatformSuperAdmin)
			return;

		if (_currentUser.UserId is not { } userId || userId == Guid.Empty)
			throw new ForbiddenException("The current user does not have valid ATS access.");

		// Same predicate the login claims provider applies: at least one active
		// UserDetails row whose role is active. The repository call is cached per
		// user and evicted by EditUserAsync, so disabling a user takes effect on
		// their next request.
		var activeRoleIds = await _userRepository.GetActiveUserRoleIdsAsync(userId, cancellationToken);
		if (activeRoleIds.Count == 0)
			throw new ForbiddenException("The current user does not have valid ATS access.");
	}
}
