namespace ATS.Data.Repository.Administration.Roles;

public sealed class RoleRepository : IRoleRepository
{
	private readonly ATSDBContext _dbContext;

	public RoleRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(PaginationRequest request, CancellationToken cancellationToken) => GetPageAsync(request, false, cancellationToken);
	public Task<PaginatedResult<RoleDetailsDTO>> SearchRolesAsync(PaginationRequest request, CancellationToken cancellationToken) => GetPageAsync(request, true, cancellationToken);

	public async Task<bool> AddRoleAsync(AddRoleDTO dto)
	{
		var now = DateTime.UtcNow;
		await _dbContext.RoleDetails.AddAsync(new RoleDetails
		{
			RoleName = dto.RoleName!, RoleDescription = dto.RoleDescription!, IsActive = dto.IsActive,
			CreatedAt = now, UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
		return true;
	}

	public Task<RoleDetails?> GetRoleAsync(int roleId) =>
		_dbContext.RoleDetails.AsNoTracking().FirstOrDefaultAsync(role => role.RoleId == roleId);

	public async Task<RoleDetails> EditRoleAsync(RoleDetails role)
	{
		_dbContext.RoleDetails.Update(role);
		await _dbContext.SaveChangesAsync();
		return role;
	}

	private async Task<PaginatedResult<RoleDetailsDTO>> GetPageAsync(PaginationRequest request, bool search, CancellationToken cancellationToken)
	{
		var query = _dbContext.RoleDetails.AsNoTracking();
		if (search)
			query = query.Where(role => EF.Functions.ILike(role.RoleName, $"%{request.SearchTerm}%") || EF.Functions.ILike(role.RoleDescription, $"%{request.SearchTerm}%"));
		var count = await query.CountAsync(cancellationToken);
		var items = await query.OrderBy(role => role.RoleName).Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
			.Select(role => new RoleDetailsDTO
			{
				RoleId = role.RoleId, RoleName = role.RoleName, RoleDescription = role.RoleDescription,
				IsActive = role.IsActive, CreatedAt = role.CreatedAt, UpdatedAt = role.UpdatedAt
			}).ToListAsync(cancellationToken);
		return new PaginatedResult<RoleDetailsDTO>(request.PageIndex, request.PageSize, count, items);
	}
}
