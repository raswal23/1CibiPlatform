namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var usersQuery = _dbcontext
				.AuthAttempts
				.Where(aa => aa.LockReleaseAt > DateTime.UtcNow)
				.AsNoTracking();
	
			var totalRecords = await usersQuery.LongCountAsync(cancellationToken);
	
			var lockedUsers = await usersQuery
							.OrderBy(a => a.UserId)
							.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
							.Take(paginationRequest.PageSize)
							.Select(aa => new AuthAttempts
							{
								LockReleaseAt = aa.LockReleaseAt,
								CreatedAt = aa.CreatedAt,
								Email = aa.Email,
								UserId = aa.UserId
							})
							.ToListAsync(cancellationToken);
	
			return new PaginatedResult<AuthAttempts>
			(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				totalRecords,
				lockedUsers
			);
		}
	
	public async Task<AuthAttempts> GetLockedUserAsync(Guid userId)
		{
			var lockedUser = await _dbcontext.AuthAttempts
						 .FirstOrDefaultAsync(aa => aa.UserId == userId);
			return lockedUser!;
		}
	
	public async Task<PaginatedResult<AuthAttempts>> SearchLockedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var usersQuery = _dbcontext.AuthAttempts
				    .AsNoTracking()
					.Where(aa => aa.LockReleaseAt > DateTime.UtcNow && 
						(EF.Functions.ILike(aa.Email!, $"%{paginationRequest.SearchTerm}%")));
	
			var totalRecords = await usersQuery.CountAsync(cancellationToken);
	
			var lockedUsers = await usersQuery
						.OrderBy(aa => aa.UserId)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(aa => new AuthAttempts
						{
							LockReleaseAt = aa.LockReleaseAt,
							UserId = aa.UserId,
							Email = aa.Email,
							CreatedAt = aa.CreatedAt
						})
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<AuthAttempts>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  lockedUsers
				);
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
