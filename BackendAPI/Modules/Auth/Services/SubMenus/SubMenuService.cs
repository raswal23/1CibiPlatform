namespace Auth.Services;

public class SubMenuService : ISubMenuService
{
	private readonly ISubMenuRepository _authRepository;
	private readonly ILogger<SubMenuService> _logger;

	public SubMenuService(ISubMenuRepository authRepository,
					   ILogger<SubMenuService> logger)
	{
		_authRepository = authRepository;
		_logger = logger;
	}

	public async Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu)
	{
		var isAdded = await _authRepository.AddSubMenuAsync(subMenu);
		return isAdded;
	}

	public async Task<bool> DeleteSubMenuAsync(int SubMenuId)
	{
		var logContext = new
		{
			Action = "DeleteSubMenu",
			Step = "FetchForDelete",
			SubMenuId,
			Timestamp = DateTime.UtcNow
		};

		var subMenu = await _authRepository.GetSubMenuAsync(SubMenuId);
		if (subMenu == null)
		{
			_logger.LogError("{SubMenuId} was not found during delete operation: {@Context}", SubMenuId, logContext);
			throw new NotFoundException($"SubMenu with ID {SubMenuId} was not found.");
		}

		var isDeleted = await _authRepository.DeleteSubMenuAsync(subMenu);

		return isDeleted;
	}

	public async Task<SubMenuDTO> EditSubMenuAsync(EditSubMenuDTO subMenuDTO)
	{
		var logContext = new
		{
			Action = "EditSubMenu",
			Step = "FetchForUpdate",
			SubMenuId = subMenuDTO.SubMenuId,
			Timestamp = DateTime.UtcNow
		};

		var existingSubMenu = await _authRepository.GetSubMenuAsync(subMenuDTO.SubMenuId);
		if (existingSubMenu == null)
		{
			_logger.LogError("{SubMenuId} was not found during update operation: {@Context}", subMenuDTO!.SubMenuId, logContext);
			throw new NotFoundException($"SubMenu with ID {subMenuDTO.SubMenuId} was not found.");
		}
		existingSubMenu.SubMenuName = subMenuDTO.SubMenuName!;
		existingSubMenu.Description = subMenuDTO!.Description;
		existingSubMenu.IsActive = subMenuDTO.IsActive;

		var subMenu = await _authRepository.EditSubMenuAsync(existingSubMenu);
		return subMenu.Adapt<SubMenuDTO>();
	}

	public Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetSubMenus",
			Step = "FetchingSubMenus",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching submenus with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_authRepository.GetSubMenusAsync(paginationRequest, cancellationToken) :
			_authRepository.SearchSubMenusAsync(paginationRequest, cancellationToken);
	}
}
