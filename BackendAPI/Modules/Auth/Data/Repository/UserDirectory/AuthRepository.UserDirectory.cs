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
				UserName = string.Join(" ", new[]
				{
					user.FirstName,
					user.MiddleName,
					user.LastName
				}.Where(name => !string.IsNullOrWhiteSpace(name)))
			}).ToList();
		}
	
	public async Task<PaginatedResult<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
			PaginationRequest paginationRequest,
			CancellationToken cancellationToken)
		{
			var usersQuery = GetATSAssignedUsersQuery();
			if (!string.IsNullOrWhiteSpace(paginationRequest.SearchTerm))
			{
				var term = $"%{paginationRequest.SearchTerm.Trim()}%";
				usersQuery = usersQuery.Where(user =>
					EF.Functions.ILike(user.FirstName, term) ||
					EF.Functions.ILike(user.MiddleName!, term) ||
					EF.Functions.ILike(user.LastName, term) ||
					EF.Functions.ILike(user.Email, term));
			}
	
			var count = await usersQuery.LongCountAsync(cancellationToken);
			var users = await usersQuery
				.OrderBy(user => user.LastName)
				.ThenBy(user => user.FirstName)
				.ThenBy(user => user.Id)
				.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
				.Take(paginationRequest.PageSize)
				.Select(user => new
				{
					UserId = user.Id,
					UserEmail = user.Email,
					user.FirstName,
					user.MiddleName,
					user.LastName
				})
				.ToListAsync(cancellationToken);
	
			var data = users.Select(user => new ATSUserLookupDTO
			{
				UserId = user.UserId,
				UserEmail = user.UserEmail,
				UserName = BuildUserName(user.FirstName, user.MiddleName, user.LastName)
			}).ToArray();
	
			return new PaginatedResult<ATSUserLookupDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				count,
				data);
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
