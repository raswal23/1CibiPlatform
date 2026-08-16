namespace Auth.Data.Repository;

public interface ILoginRepository : ILockoutRepository, IRefreshTokenRepository
{
	Task<LoginDTO> GetUserDataAsync(LoginWebCred cred);
	Task<bool> SaveUserAsync(Authusers user);
}
