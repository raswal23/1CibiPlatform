namespace ATS.Services;

public class ModuleManagementService : IModuleManagementService
{
	private readonly IModuleRepository _moduleRepository;
	private readonly ILogger<ModuleManagementService> _logger;

	public ModuleManagementService(IModuleRepository moduleRepository,
						   ILogger<ModuleManagementService> logger)
	{
		_moduleRepository = moduleRepository;
		_logger = logger;
	}

	public Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetModules",
			Step = "FetchingModules",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching modules with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_moduleRepository.GetModulesAsync(paginationRequest, cancellationToken) :
			_moduleRepository.SearchModulesAsync(paginationRequest, cancellationToken);
	}

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var logContext = new
		{
			Action = "AddModule",
			Step = "CreatingModule",
			ModuleName = moduleDTO.ModuleName,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding module: {@Context}", logContext);

		return await _moduleRepository.AddModuleAsync(moduleDTO);
	}

	public async Task<ModuleDetailsDTO> EditModuleAsync(EditModuleDTO moduleDTO)
	{
		var logContext = new
		{
			Action = "EditModule",
			Step = "FetchForUpdate",
			ModuleId = moduleDTO.ModuleId,
			Timestamp = DateTime.UtcNow
		};

		var existingModule = await _moduleRepository.GetModuleAsync(moduleDTO.ModuleId);
		if (existingModule == null)
		{
			_logger.LogError("{ModuleId} was not found during update operation: {@Context}", moduleDTO.ModuleId, logContext);
			throw new NotFoundException($"Module with ID {moduleDTO.ModuleId} was not found.");
		}

		existingModule.ModuleName = moduleDTO.ModuleName!;
		existingModule.ModuleDescription = moduleDTO.ModuleDescription!;
		existingModule.IsActive = moduleDTO.IsActive;
		existingModule.UpdatedAt = DateTime.UtcNow;

		var module = await _moduleRepository.EditModuleAsync(existingModule);
		return module.Adapt<ModuleDetailsDTO>();
	}
}
