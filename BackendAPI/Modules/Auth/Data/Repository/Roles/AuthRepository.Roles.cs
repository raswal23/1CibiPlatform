namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PaginatedResult<RolesDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var totalRecords = await _dbcontext
				.AuthRoles
				.AsNoTracking()
				.LongCountAsync(cancellationToken);
	
			var roles = await _dbcontext.AuthRoles
						.OrderBy(asr => asr.RoleId)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(asr => new RolesDTO(
							asr.RoleId,
							asr.RoleName,
							asr.Description ?? ""))
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<RolesDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  roles
				);
		}
	
	public async Task<PaginatedResult<RolesDTO>> SearchRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
	
			var rolesQuery = _dbcontext.AuthRoles
					.AsNoTracking()
					.Where(ar =>
						(EF.Functions.ILike(ar.RoleName, $"%{paginationRequest.SearchTerm}%") ||
						 EF.Functions.ILike(ar.Description!, $"%{paginationRequest.SearchTerm}%")));
	
			var totalRecords = await rolesQuery.CountAsync(cancellationToken);
	
			var roles = await rolesQuery
						.OrderBy(ar => ar.RoleId)
						.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
						.Take(paginationRequest.PageSize)
						.Select(ar => new RolesDTO(
							ar.RoleId,
							ar.RoleName,
							ar.Description ?? ""))
						.ToListAsync(cancellationToken);
	
			return new PaginatedResult<RolesDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  roles
				);
		}
	
	public async Task<bool> AddRoleAsync(AddRoleDTO role)
		{
			var authRole = new AuthRole
			{
				RoleName = role.RoleName!,
				Description = role.Description,
				CreatedAt = DateTime.UtcNow,
			};
			var isAdded = await _dbcontext.AuthRoles.AddAsync(authRole);
			await _dbcontext.SaveChangesAsync();
			return true;
		}
	
	public async Task<bool> DeleteRoleAsync(AuthRole role)
		{
			var isDeleted = _dbcontext.AuthRoles.Remove(role);
			await _dbcontext.SaveChangesAsync();
			return true;
		}
	
	public async Task<AuthRole> EditRoleAsync(AuthRole role)
		{
			_dbcontext.AuthRoles.Update(role);
			await _dbcontext.SaveChangesAsync();
			return role;
		}
	
	public async Task<AuthRole> GetRoleAsync(int roleId)
		{
			var role = await _dbcontext.AuthRoles
			.FirstOrDefaultAsync(x => x.RoleId == roleId);
	
			return role!;
		}
}
