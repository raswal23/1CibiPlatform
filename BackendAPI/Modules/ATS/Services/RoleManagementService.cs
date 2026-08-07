namespace ATS.Services;

public class RoleManagementService : IRoleManagementService
{
	private readonly IATSRepository _atsRepository;
	private readonly ILogger<RoleManagementService> _logger;

	public RoleManagementService(IATSRepository atsRepository,
						 ILogger<RoleManagementService> logger)
	{
		_atsRepository = atsRepository;
		_logger = logger;
	}

	public Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetRoles",
			Step = "FetchingRoles",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching roles with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_atsRepository.GetRolesAsync(paginationRequest, cancellationToken) :
			_atsRepository.SearchRolesAsync(paginationRequest, cancellationToken);
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO roleDTO)
	{
		var logContext = new
		{
			Action = "AddRole",
			Step = "CreatingRole",
			RoleName = roleDTO.RoleName,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding role: {@Context}", logContext);

		return await _atsRepository.AddRoleAsync(roleDTO);
	}

	public async Task<RoleDetailsDTO> EditRoleAsync(EditRoleDTO roleDTO)
	{
		var logContext = new
		{
			Action = "EditRole",
			Step = "FetchForUpdate",
			RoleId = roleDTO.RoleId,
			Timestamp = DateTime.UtcNow
		};

		var existingRole = await _atsRepository.GetRoleAsync(roleDTO.RoleId);
		if (existingRole == null)
		{
			_logger.LogError("{RoleId} was not found during update operation: {@Context}", roleDTO.RoleId, logContext);
			throw new NotFoundException($"Role with ID {roleDTO.RoleId} was not found.");
		}

		existingRole.RoleName = roleDTO.RoleName!;
		existingRole.RoleDescription = roleDTO.RoleDescription!;
		existingRole.IsActive = roleDTO.IsActive;
		existingRole.UpdatedAt = DateTime.UtcNow;

		var role = await _atsRepository.EditRoleAsync(existingRole);
		return role.Adapt<RoleDetailsDTO>();
	}
}
