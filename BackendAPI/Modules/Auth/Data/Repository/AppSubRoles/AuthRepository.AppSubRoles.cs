namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Keyset page over the joined AppSubRoles rows ordered by AppRoleId (unique PK).
	// The search filter, seek predicate, and ordering compose on the pre-projection
	// join shape — EF cannot translate member access on the constructor-projected
	// DTO after the left joins. Pure query — the service decodes the cursor and
	// mints the next one.
	public Task<List<AppSubRolesDTO>> GetAppSubRolesPageAsync(string? searchTerm, int? afterAppRoleId, int take, CancellationToken cancellationToken) =>
		BuildAppSubRolesQuery(searchTerm, afterAppRoleId).Take(take).ToListAsync(cancellationToken);

	public Task<long> CountAppSubRolesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildAppSubRolesQuery(searchTerm, afterAppRoleId: null).LongCountAsync(cancellationToken);

	private IQueryable<AppSubRolesDTO> BuildAppSubRolesQuery(string? searchTerm, int? afterAppRoleId)
		{
			var baseQuery =
				from asr in _dbcontext.AuthUserAppRoles.AsNoTracking()
				join u in _dbcontext.AuthUsers.AsNoTracking()
					on asr.UserId equals u.Id into uGroup
				from user in uGroup.DefaultIfEmpty()

				join r in _dbcontext.AuthRoles.AsNoTracking()
					on asr.RoleId equals r.RoleId into rGroup
				from role in rGroup.DefaultIfEmpty()

				join a in _dbcontext.AuthApplications.AsNoTracking()
					on asr.AppId equals a.AppId into aGroup
				from app in aGroup.DefaultIfEmpty()

				join s in _dbcontext.AuthSubmenu.AsNoTracking()
					on asr.Submenu equals s.SubMenuId into sGroup
				from sub in sGroup.DefaultIfEmpty()

				select new { asr, user, role, app, sub };

			if (!string.IsNullOrEmpty(searchTerm))
			{
				var search = searchTerm.Trim().ToLower();
				baseQuery = baseQuery.Where(x =>
					EF.Functions.ILike(x.sub.SubMenuName, $"%{search}%") ||
					EF.Functions.ILike(x.role.RoleName!, $"%{search}%") ||
					EF.Functions.ILike(x.user.Email!, $"%{search}%") ||
					EF.Functions.ILike(x.app.AppName!, $"%{search}%"));
			}

			if (afterAppRoleId.HasValue)
				baseQuery = baseQuery.Where(x => x.asr.AppRoleId > afterAppRoleId.Value);

			return baseQuery
				.OrderBy(x => x.asr.AppRoleId)
				.Select(x => new AppSubRolesDTO(
					x.asr.AppRoleId,
					x.asr.UserId,
					x.user.Email,
					x.asr.AppId,
					x.app.AppName,
					x.asr.Submenu,
					x.sub.SubMenuName,
					x.asr.RoleId,
					x.role.RoleName
				));
		}
	
	public async Task<AuthUserAppRole> GetAppSubRoleAsync(int appSubRoleId)
		{
			var appSubRole = await _dbcontext.AuthUserAppRoles
			.FirstOrDefaultAsync(x => x.AppRoleId == appSubRoleId);
	
			return appSubRole!;
		}
	
	public async Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
		{
			var authUserAppRole = new AuthUserAppRole
			{
				UserId = appSubRole.UserId!,
				AppId = appSubRole.AppId,
				Submenu = appSubRole.SubMenuId,
				RoleId = appSubRole.RoleId,
				AssignedBy = appSubRole.AssignedBy,
				AssignedAt = DateTime.UtcNow,
			};
			var isAdded = await _dbcontext.AuthUserAppRoles.AddAsync(authUserAppRole);
			await _dbcontext.SaveChangesAsync();
			return true;
		}
	
	public async Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole)
		{
			var isDeleted = _dbcontext.AuthUserAppRoles.Remove(appSubRole);
			await _dbcontext.SaveChangesAsync();
			return true;
		}
	
	public async Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole)
		{
			_dbcontext.AuthUserAppRoles.Update(appSubRole);
			await _dbcontext.SaveChangesAsync();
			return appSubRole;
		}
}
