namespace Auth.Services;

public class RoleService : IRoleService
{
	private readonly IRoleRepository _authRepository;
	private readonly ILogger<RoleService> _logger;

	public RoleService(IRoleRepository authRepository,
					   ILogger<RoleService> logger)
	{
		_authRepository = authRepository;
		_logger = logger;
	}

	public async Task<KeysetPaginatedResult<RolesDTO>> GetRolesAsync(
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

		_logger.LogInformation("Fetching role with pagination: {@Context}", logContext);

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		int? afterRoleId = int.TryParse(fields?[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var roleId) ? roleId : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _authRepository.GetRolesPageAsync(paginationRequest.SearchTerm, afterRoleId, pageSize + 1, cancellationToken);
		var (roles, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(roles[^1].roleId.ToString(CultureInfo.InvariantCulture))
			: null;
		long? totalCount = afterRoleId is null
			? await _authRepository.CountRolesAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<RolesDTO>(roles, nextCursor, totalCount);
	}

	public async Task<bool> DeleteRoleAsync(int RoleId)
	{
		var logContext = new
		{
			Action = "DeleteRole",
			Step = "FetchForDelete",
			RoleId,
			Timestamp = DateTime.UtcNow
		};

		var role = await _authRepository.GetRoleAsync(RoleId);
		if (role == null)
		{
			_logger.LogError("{RoleId} was not found during delete operation: {@Context}", RoleId, logContext);
			throw new NotFoundException($"Role with ID {RoleId} was not found.");
		}
		var isDeleted = await _authRepository.DeleteRoleAsync(role);
		return isDeleted;
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO role)
	{
		var isAdded = await _authRepository.AddRoleAsync(role);
		return isAdded;
	}

	public async Task<RoleDTO> EditRoleAsync(EditRoleDTO roleDTO)
	{
		var logContext = new
		{
			Action = "EditRole",
			Step = "FetchForUpdate",
			roleDTO.RoleId,
			Timestamp = DateTime.UtcNow
		};

		var existingRole = await _authRepository.GetRoleAsync(roleDTO.RoleId);
		if (existingRole == null)
		{
			_logger.LogError("{RoleId} was not found during update operation: {@Context}", roleDTO.RoleId, logContext);
			throw new NotFoundException($"Role with ID {roleDTO.RoleId} was not found.");
		}

		existingRole.RoleName = roleDTO.RoleName!;
		existingRole.Description = roleDTO.Description;

		var role = await _authRepository.EditRoleAsync(existingRole);
		return role.Adapt<RoleDTO>();
	}
}
