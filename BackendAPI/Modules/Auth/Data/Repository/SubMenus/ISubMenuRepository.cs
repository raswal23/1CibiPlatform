namespace Auth.Data.Repository;

public interface ISubMenuRepository
{
	Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<AuthSubMenu> GetSubMenuAsync(int applicationId);
	Task<PaginatedResult<SubMenusDTO>> SearchSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu);
	Task<AuthSubMenu> EditSubMenuAsync(AuthSubMenu subMenu);
	Task<bool> DeleteSubMenuAsync(AuthSubMenu subMenu);
}
