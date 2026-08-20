namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<List<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
			CancellationToken cancellationToken)
		{
			var users = await GetATSAssignedUsersQuery()
				.OrderBy(user => user.LastName)
				.ThenBy(user => user.FirstName)
				.ThenBy(user => user.Id)
				.Select(user => new
				{
					UserId = user.Id,
					UserEmail = user.Email,
					user.FirstName,
					user.MiddleName,
					user.LastName
				})
				.ToListAsync(cancellationToken);
	
			return users.Select(user => new ATSUserLookupDTO
			{
				UserId = user.UserId,
				UserEmail = user.UserEmail,
				FirstName = user.FirstName,
				MiddleName = user.MiddleName,
				LastName = user.LastName,
				UserName = BuildUserName(user.FirstName, user.MiddleName, user.LastName)
			}).ToList();
		}
	
	// Keyset over (LastName, FirstName, Id); LastName/FirstName are required
	// columns, Id is the unique tiebreaker. string.Compare and Guid.CompareTo
	// translate to server-side comparisons under the database collation. Pure
	// query — AuthQueries decodes the cursor, mints the next one, and joins the
	// name parts into UserName (not EF-translatable), so UserName is left empty here.
	public async Task<List<ATSUserLookupDTO>> GetATSAssignedUsersPageAsync(
			string? searchTerm,
			string? afterLastName,
			string? afterFirstName,
			Guid? afterId,
			int take,
			CancellationToken cancellationToken)
		{
			var usersQuery = BuildATSAssignedUsersQuery(searchTerm);
			if (afterLastName is not null && afterFirstName is not null && afterId.HasValue)
			{
				var cId = afterId.Value;
				usersQuery = usersQuery.Where(user =>
					string.Compare(user.LastName, afterLastName) > 0
					|| (user.LastName == afterLastName && (string.Compare(user.FirstName, afterFirstName) > 0
					|| (user.FirstName == afterFirstName && user.Id.CompareTo(cId) > 0))));
			}

			return await usersQuery
				.OrderBy(user => user.LastName)
				.ThenBy(user => user.FirstName)
				.ThenBy(user => user.Id)
				.Take(take)
				.Select(user => new ATSUserLookupDTO
				{
					UserId = user.Id,
					UserEmail = user.Email,
					FirstName = user.FirstName,
					MiddleName = user.MiddleName,
					LastName = user.LastName
				})
				.ToListAsync(cancellationToken);
		}

	public Task<long> CountATSAssignedUsersAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildATSAssignedUsersQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<Authusers> BuildATSAssignedUsersQuery(string? searchTerm)
		{
			var usersQuery = GetATSAssignedUsersQuery();
			if (!string.IsNullOrWhiteSpace(searchTerm))
			{
				var term = $"%{searchTerm.Trim()}%";
				usersQuery = usersQuery.Where(user =>
					EF.Functions.ILike(user.FirstName, term) ||
					EF.Functions.ILike(user.MiddleName!, term) ||
					EF.Functions.ILike(user.LastName, term) ||
					EF.Functions.ILike(user.Email, term));
			}

			return usersQuery;
		}
	
	public async Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(
			Guid userId,
			CancellationToken cancellationToken)
		{
			var user = await GetATSAssignedUsersQuery()
				.Where(item => item.Id == userId)
				.Select(item => new
				{
					UserId = item.Id,
					UserEmail = item.Email,
					item.FirstName,
					item.MiddleName,
					item.LastName
				})
				.SingleOrDefaultAsync(cancellationToken);
	
			return user is null
				? null
				: new ATSUserLookupDTO
				{
					UserId = user.UserId,
					UserEmail = user.UserEmail,
					FirstName = user.FirstName,
					MiddleName = user.MiddleName,
					LastName = user.LastName,
					UserName = BuildUserName(user.FirstName, user.MiddleName, user.LastName)
				};
		}
	
	public async Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(
			IReadOnlyCollection<string> emails,
			CancellationToken cancellationToken)
		{
			if (emails.Count == 0)
			{
				return new Dictionary<string, Guid>();
			}
	
			var users = await _dbcontext.AuthUsers
				.AsNoTracking()
				.Where(user => emails.Contains(user.Email))
				.Select(user => new { user.Email, user.Id })
				.ToListAsync(cancellationToken);
	
			return users.ToDictionary(user => user.Email, user => user.Id);
		}
	
	private IQueryable<Authusers> GetATSAssignedUsersQuery() =>
			_dbcontext.AuthUsers
				.AsNoTracking()
				.Where(user =>
					user.IsApproved &&
					user.IsActive &&
					_dbcontext.AuthUserAppRoles.Any(role =>
						role.UserId == user.Id && role.Submenu == 7));
	
	private static string BuildUserName(string firstName, string? middleName, string lastName) =>
			string.Join(" ", new[] { firstName, middleName, lastName }
				.Where(name => !string.IsNullOrWhiteSpace(name)));
}
