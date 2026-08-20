namespace Auth.Data.Repository;

public interface ISubMenuRepository
{
	Task<List<SubMenusDTO>> GetSubMenusPageAsync(string? searchTerm, int? afterSubMenuId, int take, CancellationToken cancellationToken);
	Task<long> CountSubMenusAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<AuthSubMenu> GetSubMenuAsync(int applicationId);
	Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu);
	Task<AuthSubMenu> EditSubMenuAsync(AuthSubMenu subMenu);
	Task<bool> DeleteSubMenuAsync(AuthSubMenu subMenu);
}
