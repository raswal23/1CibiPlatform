namespace Auth.Data.Cache;

public partial class AuthCacheRepository : IAuthRepository
{
	private readonly IAuthRepository _authRepository;

	private readonly HybridCache _hybridCache;

	private const string UsersTag = "users";

	private const string SubMenusTag = "submenus";

	private const string ApplicationsTag = "applications";

	private const string AppSubRolesTag = "appsubroles";

	private const string RolesTag = "roles";

	private const string UnApprovedUsersTag = "unapprovedusers";

	private const string LockedUsersTag = "lockedusers";

	private readonly string UserLockoutDate = "userlockoutdate";

	private readonly string _userAttemptTag = "user_attempt";

	public AuthCacheRepository(
			IAuthRepository authRepository,
			HybridCache hybridCache)
		{
			_authRepository = authRepository;
			_hybridCache = hybridCache;
		}
}
