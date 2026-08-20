namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Shared keyset page over AuthUsers ordered by Id (unique PK, so one cursor
	// field). Pure queries — the service decodes the cursor and mints the next one.
	public Task<List<UsersDTO>> GetUsersPageAsync(string? searchTerm, Guid? afterId, int take, CancellationToken cancellationToken) =>
		GetPageAsync(BuildUsersQuery(searchTerm), afterId, take, cancellationToken);

	public Task<long> CountUsersAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildUsersQuery(searchTerm).LongCountAsync(cancellationToken);

	public Task<List<UsersDTO>> GetUnapprovedUsersPageAsync(string? searchTerm, Guid? afterId, int take, CancellationToken cancellationToken) =>
		GetPageAsync(BuildUnapprovedUsersQuery(searchTerm), afterId, take, cancellationToken);

	public Task<long> CountUnapprovedUsersAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildUnapprovedUsersQuery(searchTerm).LongCountAsync(cancellationToken);

	public async Task<Authusers> GetUserAsync(string email)
		{
			var user = await _dbcontext.AuthUsers.FirstOrDefaultAsync(u => u.Email == email);

			return user!;
		}

	private IQueryable<Authusers> BuildUsersQuery(string? searchTerm)
	{
		var usersQuery = _dbcontext.AuthUsers
			.AsNoTracking()
			.Where(au => au.IsApproved == true && au.IsActive);

		if (!string.IsNullOrEmpty(searchTerm))
			usersQuery = usersQuery.Where(au =>
				EF.Functions.ILike(au.FirstName, $"%{searchTerm}%") ||
				EF.Functions.ILike(au.MiddleName!, $"%{searchTerm}%") ||
				EF.Functions.ILike(au.LastName, $"%{searchTerm}%") ||
				EF.Functions.ILike(au.Email, $"%{searchTerm}%"));

		return usersQuery;
	}

	private IQueryable<Authusers> BuildUnapprovedUsersQuery(string? searchTerm)
	{
		var usersQuery = _dbcontext.AuthUsers
			.AsNoTracking()
			.Where(au => au.IsApproved == false && au.IsActive);

		if (!string.IsNullOrEmpty(searchTerm))
			usersQuery = usersQuery.Where(au => EF.Functions.ILike(au.Email, $"%{searchTerm}%"));

		return usersQuery;
	}

	private static Task<List<UsersDTO>> GetPageAsync(
			IQueryable<Authusers> usersQuery,
			Guid? afterId,
			int take,
			CancellationToken cancellationToken)
		{
			if (afterId.HasValue)
				usersQuery = usersQuery.Where(au => au.Id.CompareTo(afterId.Value) > 0);

			return usersQuery
						.OrderBy(a => a.Id)
						.Take(take)
						.Select(au => new UsersDTO(
							au.Id,
							au.Email,
							au.FirstName,
							au.MiddleName ?? "",
							au.LastName,
							au.IsApproved))
						.ToListAsync(cancellationToken);
		}

	public async Task<Authusers> GetRawUserAsync(Guid id)
		{
			return await _dbcontext.AuthUsers
						 .Where(au => au.Id == id && au.IsActive)
						 .FirstOrDefaultAsync();
		}

	public async Task<Authusers> EditUserAsync(Authusers user)
		{
			_dbcontext.AuthUsers.Update(user);
			await _dbcontext.SaveChangesAsync();

			return user;
		}
}
