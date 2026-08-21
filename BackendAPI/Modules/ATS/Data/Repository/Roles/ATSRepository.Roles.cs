namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset ordered by RoleName (unique index) — pure query; the service decodes
	// the cursor and mints the next one.
	public async Task<List<RoleDetailsDTO>> GetRolesPageAsync(string? searchTerm, string? afterRoleName, int take, CancellationToken cancellationToken)
	{
		var query = BuildRolesQuery(searchTerm);
		if (afterRoleName is not null)
			query = query.Where(role => string.Compare(role.RoleName, afterRoleName) > 0);

		return await query.OrderBy(role => role.RoleName).Take(take)
			.Select(role => new RoleDetailsDTO
			{
				RoleId = role.RoleId,
				RoleName = role.RoleName,
				RoleDescription = role.RoleDescription,
				IsActive = role.IsActive,
				CreatedAt = role.CreatedAt,
				UpdatedAt = role.UpdatedAt
			}).ToListAsync(cancellationToken);
	}

	public Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildRolesQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<RoleDetails> BuildRolesQuery(string? searchTerm)
	{
		var query = _dbcontext.RoleDetails.AsNoTracking();
		if (!string.IsNullOrEmpty(searchTerm))
			query = query.Where(role => EF.Functions.ILike(role.RoleName, $"%{searchTerm}%") || EF.Functions.ILike(role.RoleDescription, $"%{searchTerm}%"));
		return query;
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO dto)
	{
		var now = DateTime.UtcNow;
		await _dbcontext.RoleDetails.AddAsync(new RoleDetails
		{
			RoleName = dto.RoleName!,
			RoleDescription = dto.RoleDescription!,
			IsActive = dto.IsActive,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public Task<RoleDetails?> GetRoleAsync(int roleId) =>
		_dbcontext.RoleDetails.AsNoTracking().FirstOrDefaultAsync(role => role.RoleId == roleId);

	public async Task<RoleDetails> EditRoleAsync(RoleDetails role)
	{
		_dbcontext.RoleDetails.Update(role);
		await _dbcontext.SaveChangesAsync();
		return role;
	}
}
