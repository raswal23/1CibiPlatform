namespace ATS.Services.Settings.RoleManagement;

public class RoleManagementService : IRoleManagementService
{
	private readonly IRoleRepository _roleRepository;
	private readonly ILogger<RoleManagementService> _logger;

	public RoleManagementService(IRoleRepository roleRepository,
						 ILogger<RoleManagementService> logger)
	{
		_roleRepository = roleRepository;
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
			_roleRepository.GetRolesAsync(paginationRequest, cancellationToken) :
			_roleRepository.SearchRolesAsync(paginationRequest, cancellationToken);
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

		return await _roleRepository.AddRoleAsync(roleDTO);
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

		var existingRole = await _roleRepository.GetRoleAsync(roleDTO.RoleId);
		if (existingRole == null)
		{
			_logger.LogError("{RoleId} was not found during update operation: {@Context}", roleDTO.RoleId, logContext);
			throw new NotFoundException($"Role with ID {roleDTO.RoleId} was not found.");
		}

		existingRole.RoleName = roleDTO.RoleName!;
		existingRole.RoleDescription = roleDTO.RoleDescription!;
		existingRole.IsActive = roleDTO.IsActive;
		existingRole.UpdatedAt = DateTime.UtcNow;

		var role = await _roleRepository.EditRoleAsync(existingRole);
		return role.Adapt<RoleDetailsDTO>();
	}
}
