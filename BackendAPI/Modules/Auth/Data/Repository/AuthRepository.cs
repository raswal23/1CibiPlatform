namespace Auth.Data.Repository;

public partial class AuthRepository : IAuthRepository
{
	private readonly AuthApplicationDbContext _dbcontext;

	public AuthRepository(AuthApplicationDbContext dbcontext)
	{
		this._dbcontext = dbcontext;
	}
}
