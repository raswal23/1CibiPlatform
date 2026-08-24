namespace Auth.Services;

public interface ISubMenuService
{
	Task<KeysetPaginatedResult<SubMenusDTO>> GetSubMenusAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteSubMenuAsync(int AppId);

	Task<SubMenuDTO> EditSubMenuAsync(EditSubMenuDTO subMenuDTO);
	Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu);
}
