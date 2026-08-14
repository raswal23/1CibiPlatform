namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
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
	
				orderby asr.AppRoleId
				select new AppSubRolesDTO(
					asr.AppRoleId,
					asr.UserId,
					user.Email,
					asr.AppId,
					app.AppName,
					asr.Submenu,
					sub.SubMenuName,
					asr.RoleId,
					role.RoleName
				);
	
			var totalRecords = await baseQuery.LongCountAsync(cancellationToken);
	
			var applications = await baseQuery
				.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
				.Take(paginationRequest.PageSize)
				.ToListAsync(cancellationToken);
	
			return new PaginatedResult<AppSubRolesDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				totalRecords,
				applications
			);
		}
	
	public async Task<PaginatedResult<AppSubRolesDTO>> SearchAppSubRoleAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
		{
			var search = paginationRequest.SearchTerm?.Trim().ToLower() ?? "";
	
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
	
				select new
				{
					asr,
					user,
					role,
					app,
					sub
				};
	
			baseQuery = baseQuery
				.AsNoTracking()
				.Where(x =>
				EF.Functions.ILike(x.sub.SubMenuName, $"%{search}%") ||
				EF.Functions.ILike(x.role.RoleName!, $"%{search}%") ||
				EF.Functions.ILike(x.user.Email!, $"%{search}%") ||
				EF.Functions.ILike(x.app.AppName!, $"%{search}%")
			);
	
			var totalRecords = await baseQuery.CountAsync(cancellationToken);
	
			var results = await baseQuery
				.OrderBy(x => x.asr.AppRoleId)
				.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
				.Take(paginationRequest.PageSize)
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
				))
				.ToListAsync(cancellationToken);
	
			return new PaginatedResult<AppSubRolesDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				totalRecords,
				results
			);
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
