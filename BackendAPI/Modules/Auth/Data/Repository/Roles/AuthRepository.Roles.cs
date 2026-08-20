namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Keyset page over AuthRoles ordered by RoleId (unique PK). Pure query — the
	// service decodes the cursor and mints the next one.
	public async Task<List<RolesDTO>> GetRolesPageAsync(string? searchTerm, int? afterRoleId, int take, CancellationToken cancellationToken)
		{
			var rolesQuery = BuildRolesQuery(searchTerm);
			if (afterRoleId.HasValue)
				rolesQuery = rolesQuery.Where(ar => ar.RoleId > afterRoleId.Value);

			return await rolesQuery
						.OrderBy(ar => ar.RoleId)
						.Take(take)
						.Select(ar => new RolesDTO(
							ar.RoleId,
							ar.RoleName,
							ar.Description ?? ""))
						.ToListAsync(cancellationToken);
		}

	public Task<long> CountRolesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildRolesQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<AuthRole> BuildRolesQuery(string? searchTerm)
	{
		var rolesQuery = _dbcontext.AuthRoles.AsNoTracking();
		if (!string.IsNullOrEmpty(searchTerm))
			rolesQuery = rolesQuery.Where(ar =>
				EF.Functions.ILike(ar.RoleName, $"%{searchTerm}%") ||
				EF.Functions.ILike(ar.Description!, $"%{searchTerm}%"));
		return rolesQuery;
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
