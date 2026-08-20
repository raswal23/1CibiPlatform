namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Keyset page over AuthAttempts ordered by UserId (the table's PK). The
	// LockReleaseAt > now filter is a moving window — locks expiring mid-walk simply
	// drop out of later pages (keyset never duplicates rows). Pure query — the
	// service decodes the cursor and mints the next one.
	public async Task<List<AuthAttempts>> GetLockedUsersPageAsync(string? searchTerm, Guid? afterUserId, int take, CancellationToken cancellationToken)
		{
			var usersQuery = BuildLockedUsersQuery(searchTerm);
			if (afterUserId.HasValue)
				usersQuery = usersQuery.Where(aa => aa.UserId.CompareTo(afterUserId.Value) > 0);

			return await usersQuery
						.OrderBy(aa => aa.UserId)
						.Take(take)
						.Select(aa => new AuthAttempts
						{
							LockReleaseAt = aa.LockReleaseAt,
							CreatedAt = aa.CreatedAt,
							Email = aa.Email,
							UserId = aa.UserId
						})
						.ToListAsync(cancellationToken);
		}

	public Task<long> CountLockedUsersAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildLockedUsersQuery(searchTerm).LongCountAsync(cancellationToken);

	public async Task<AuthAttempts> GetLockedUserAsync(Guid userId)
		{
			var lockedUser = await _dbcontext.AuthAttempts
						 .FirstOrDefaultAsync(aa => aa.UserId == userId);
			return lockedUser!;
		}

	private IQueryable<AuthAttempts> BuildLockedUsersQuery(string? searchTerm)
	{
		var usersQuery = _dbcontext.AuthAttempts
			.AsNoTracking()
			.Where(aa => aa.LockReleaseAt > DateTime.UtcNow);

		if (!string.IsNullOrEmpty(searchTerm))
			usersQuery = usersQuery.Where(aa => EF.Functions.ILike(aa.Email!, $"%{searchTerm}%"));

		return usersQuery;
	}

	public async Task<bool> SaveLockedUserAsync(AuthAttempts userAttempt)
		{
			await _dbcontext.AuthAttempts.AddAsync(userAttempt);

			var result = await _dbcontext.SaveChangesAsync();

			return true;

		}

	public async Task<bool> DeleteLockedUserAsync(AuthAttempts lockedUser)
		{
			await _dbcontext.AuthAttempts.
				  Where(aa => aa.UserId == lockedUser.UserId).ExecuteDeleteAsync();

			return true;
		}
}
