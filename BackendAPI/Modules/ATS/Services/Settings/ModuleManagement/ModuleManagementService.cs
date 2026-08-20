namespace ATS.Services.Settings.ModuleManagement;

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

	public async Task<KeysetPaginatedResult<ModuleDetailsDTO>> GetModulesAsync(
		KeysetPaginationRequest paginationRequest,
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

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		var afterModuleName = fields?[0];
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _moduleRepository.GetModulesPageAsync(paginationRequest.SearchTerm, afterModuleName, pageSize + 1, cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore ? CursorCodec.Encode(items[^1].ModuleName) : null;
		long? totalCount = afterModuleName is null
			? await _moduleRepository.CountModulesAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<ModuleDetailsDTO>(items, nextCursor, totalCount);
	}

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var moduleName = moduleDTO.ModuleName!.Trim();
		if (await _moduleRepository.ModuleNameExistsAsync(moduleName))
			throw new BadRequestException($"Module '{moduleName}' already exists.");

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
