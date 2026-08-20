namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Keyset page over AuthSubmenu ordered by SubMenuId (unique PK). Pure query —
	// the service decodes the cursor and mints the next one.
	public async Task<List<SubMenusDTO>> GetSubMenusPageAsync(string? searchTerm, int? afterSubMenuId, int take, CancellationToken cancellationToken)
		{
			var subMenusQuery = BuildSubMenusQuery(searchTerm);
			if (afterSubMenuId.HasValue)
				subMenusQuery = subMenusQuery.Where(asm => asm.SubMenuId > afterSubMenuId.Value);

			return await subMenusQuery
							.OrderBy(asm => asm.SubMenuId)
							.Take(take)
							.Select(asm => new SubMenusDTO(
								asm.SubMenuId,
								asm.SubMenuName,
								asm.Description ?? "",
								asm.IsActive))
							.ToListAsync(cancellationToken);
		}

	public Task<long> CountSubMenusAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildSubMenusQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<AuthSubMenu> BuildSubMenusQuery(string? searchTerm)
	{
		var subMenusQuery = _dbcontext.AuthSubmenu
			.AsNoTracking()
			.Where(asm => asm.IsActive);

		if (!string.IsNullOrEmpty(searchTerm))
			subMenusQuery = subMenusQuery.Where(asm =>
				EF.Functions.ILike(asm.SubMenuName, $"%{searchTerm}%") ||
				EF.Functions.ILike(asm.Description!, $"%{searchTerm}%"));

		return subMenusQuery;
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
