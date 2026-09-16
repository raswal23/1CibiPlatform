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

	public async Task<KeysetPaginatedResult<RoleDetailsDTO>> GetRolesAsync(
		KeysetPaginationRequest paginationRequest,
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

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		var afterRoleName = fields?[0];
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _roleRepository.GetRolesPageAsync(paginationRequest.SearchTerm, afterRoleName, pageSize + 1, cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore ? CursorCodec.Encode(items[^1].RoleName) : null;
		long? totalCount = afterRoleName is null
			? await _roleRepository.CountRolesAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<RoleDetailsDTO>(items, nextCursor, totalCount);
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

	public async Task<RoleDetailsDTO> EditRoleAsync(EditRoleDTO roleDTO, CancellationToken cancellationToken)
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

		// Check-then-write: acceptable for an admin screen; a racing assignment merely
		// produces a role disabled a moment too late.
		if (existingRole.IsActive && !roleDTO.IsActive)
		{
			var activeUsers = await _roleRepository.CountActiveUsersInRoleAsync(roleDTO.RoleId, cancellationToken);
			if (activeUsers > 0)
				throw new ConflictException(activeUsers == 1
					? "Cannot disable this role: 1 active user currently holds it."
					: $"Cannot disable this role: {activeUsers} active users currently hold it.");
		}

		existingRole.RoleName = roleDTO.RoleName!;
		existingRole.RoleDescription = roleDTO.RoleDescription!;
		existingRole.IsActive = roleDTO.IsActive;
		existingRole.UpdatedAt = DateTime.UtcNow;

		var role = await _roleRepository.EditRoleAsync(existingRole);
		return role.Adapt<RoleDetailsDTO>();
	}
}
