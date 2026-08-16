namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var usersQuery = _dbcontext
				.AuthSubmenu
				.AsNoTracking()
				.Where(asm => asm.IsActive);
	
			var totalRecords = await usersQuery.LongCountAsync(cancellationToken);
	
			var subMenus = await usersQuery
							.OrderBy(asm => asm.SubMenuId)
							.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
							.Take(paginationRequest.PageSize)
							.Select(asm => new SubMenusDTO(
								asm.SubMenuId,
								asm.SubMenuName,
								asm.Description ?? "",
								asm.IsActive))
							.ToListAsync(cancellationToken);
	
			return new PaginatedResult<SubMenusDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  subMenus
				);
		}
	
	public async Task<PaginatedResult<SubMenusDTO>> SearchSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var subMenusQuery = _dbcontext.AuthSubmenu
				    .AsNoTracking()
					.Where(asm => asm.IsActive &&
						(EF.Functions.ILike(asm.SubMenuName, $"%{paginationRequest.SearchTerm}%") ||
						 EF.Functions.ILike(asm.Description!, $"%{paginationRequest.SearchTerm}%")));
	
			var totalRecords = await subMenusQuery.CountAsync(cancellationToken);
	
			var subMenus = await subMenusQuery
							.OrderBy(asm => asm.SubMenuId)
							.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
							.Take(paginationRequest.PageSize)
							.Select(asm => new SubMenusDTO(
								asm.SubMenuId,
								asm.SubMenuName,
								asm.Description ?? "",
								asm.IsActive))
							.ToListAsync(cancellationToken);
	
			return new PaginatedResult<SubMenusDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  subMenus
				);
		}
	
	public async Task<AuthSubMenu> GetSubMenuAsync(int subMenuId)
		{
			var subMenu = await _dbcontext.AuthSubmenu
			.FirstOrDefaultAsync(x => x.SubMenuId == subMenuId);
	
			return subMenu!;
		}
	
	public async Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu)
		{
			var authSubMenu = new AuthSubMenu
			{
				SubMenuName = subMenu.SubMenuName!,
				Description = subMenu.Description,
				IsActive = subMenu.IsActive,
				CreatedAt = DateTime.UtcNow
			};
			var isAdded = await _dbcontext.AuthSubmenu.AddAsync(authSubMenu);
			await _dbcontext.SaveChangesAsync();
			return true;
		}
	
	public async Task<bool> DeleteSubMenuAsync(AuthSubMenu subMenu)
		{
			var isDeleted = _dbcontext.AuthSubmenu.Remove(subMenu);
			await _dbcontext.SaveChangesAsync();
			return true;
		}
	
	public async Task<AuthSubMenu> EditSubMenuAsync(AuthSubMenu subMenu)
		{
			_dbcontext.AuthSubmenu.Update(subMenu);
			await _dbcontext.SaveChangesAsync();
	
			return subMenu;
		}
}
