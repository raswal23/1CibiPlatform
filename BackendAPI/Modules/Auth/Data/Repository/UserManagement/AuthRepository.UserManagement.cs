namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PaginatedResult<UsersDTO>> GetUserAsync(
			PaginationRequest paginationRequest,
			CancellationToken cancellationToken)
		{
			var usersQuery = _dbcontext
				.AuthUsers
				.AsNoTracking()
				.Where(au => au.IsApproved == true && au.IsActive);
	
			var totalRecords = await usersQuery.LongCountAsync(cancellationToken);
	
			var users = await usersQuery
						.OrderBy(a => a.Id)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(au => new UsersDTO(
							au.Id,
							au.Email,
							au.FirstName,
							au.MiddleName ?? "",
							au.LastName,
							au.IsApproved))
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<UsersDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  users
				);
		}
	
	public async Task<PaginatedResult<UsersDTO>> GetUnapprovedUserAsync(
			PaginationRequest paginationRequest,
			CancellationToken cancellationToken)
		{
			var usersQuery = _dbcontext
				.AuthUsers
				.AsNoTracking()
				.Where(a => a.IsApproved == false && a.IsActive);
	
			var totalRecords = await usersQuery.LongCountAsync(cancellationToken);
	
			var users = await usersQuery
						.OrderBy(a => a.Id)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(au => new UsersDTO(
							au.Id,
							au.Email,
							au.FirstName,
							au.MiddleName ?? "",
							au.LastName,
							au.IsApproved))
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<UsersDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  users
				);
		}
	
	public async Task<Authusers> GetUserAsync(string email)
		{
			var user = await _dbcontext.AuthUsers.FirstOrDefaultAsync(u => u.Email == email);
	
			return user!;
		}
	
	public async Task<PaginatedResult<UsersDTO>> SearchUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
	
			var usersQuery = _dbcontext.AuthUsers
				    .AsNoTracking()
					.Where(au => au.IsApproved == true && au.IsActive &&
						(EF.Functions.ILike(au.FirstName, $"%{paginationRequest.SearchTerm}%") ||
						 EF.Functions.ILike(au.MiddleName!, $"%{paginationRequest.SearchTerm}%") ||
						 EF.Functions.ILike(au.LastName, $"%{paginationRequest.SearchTerm}%") ||
						 EF.Functions.ILike(au.Email, $"%{paginationRequest.SearchTerm}%")));
	
			var totalRecords = await usersQuery.CountAsync(cancellationToken);
	
			var users = await usersQuery
						.OrderBy(au => au.Id)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(au => new UsersDTO(
							au.Id,
							au.Email,
							au.FirstName,
							au.MiddleName ?? "",
							au.LastName,
							au.IsApproved))
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<UsersDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  users
				);
		}
	
	public async Task<PaginatedResult<UsersDTO>> SearchUnApprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
	
			var usersQuery = _dbcontext.AuthUsers
				    .AsNoTracking()
					.Where(au => au.IsApproved == false && au.IsActive &&
						(EF.Functions.ILike(au.Email, $"%{paginationRequest.SearchTerm}%")));
	
			var totalRecords = await usersQuery.CountAsync(cancellationToken);
	
			var users = await usersQuery
						.OrderBy(au => au.Id)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(au => new UsersDTO(
							au.Id,
							au.Email,
							au.FirstName,
							au.MiddleName ?? "",
							au.LastName,
							au.IsApproved))
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<UsersDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  users
				);
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
