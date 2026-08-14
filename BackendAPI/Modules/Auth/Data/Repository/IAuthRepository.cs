namespace Auth.Data.Repository;

/// <summary>
/// Aggregate repository contract retained for the cache decorator and compatibility.
/// Business services should depend on the focused contracts below instead.
/// </summary>
public interface IAuthRepository :
	IApplicationRepository,
	IAppSubRoleRepository,
	ILockoutRepository,
	ILoginRepository,
	IPasswordRecoveryRepository,
	IRefreshTokenRepository,
	IRegistrationRepository,
	IRoleRepository,
	ISubMenuRepository,
	IUserDirectoryRepository,
	IUserRepository
{
}
