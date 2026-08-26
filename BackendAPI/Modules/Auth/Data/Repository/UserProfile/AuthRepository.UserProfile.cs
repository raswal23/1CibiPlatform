namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Tracked on purpose: the profile service mutates the name columns on the
	// instance this returns and hands it straight back to UpdateProfileAsync.
	public async Task<Authusers?> GetProfileAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		return await _dbcontext.AuthUsers
			.FirstOrDefaultAsync(
				user => user.Id == userId && user.IsActive,
				cancellationToken);
	}

	public async Task<Authusers> UpdateProfileAsync(
		Authusers user,
		CancellationToken cancellationToken)
	{
		_dbcontext.AuthUsers.Update(user);

		await _dbcontext.SaveChangesAsync(cancellationToken);

		return user;
	}
}
