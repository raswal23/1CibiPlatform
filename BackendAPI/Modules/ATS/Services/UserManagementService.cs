namespace ATS.Services;

public class UserManagementService : IUserManagementService
{
	private const string SuperAdminEmail = "admin@cibi.com";
	private readonly IATSRepository _atsRepository;
	private readonly IAuthQueries _authQueries;
	private readonly ILogger<UserManagementService> _logger;

	public UserManagementService(
		IATSRepository atsRepository,
		IAuthQueries authQueries,
		ILogger<UserManagementService> logger)
	{
		_atsRepository = atsRepository;
		_authQueries = authQueries;
		_logger = logger;
	}

	public Task<IReadOnlyList<ATSUserLookupDTO>> GetAuthUsersAsync(
		CancellationToken cancellationToken)
	{
		return _authQueries.GetATSAssignedUsersAsync(cancellationToken);
	}

	public Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken)
	{
		return _atsRepository.GetUserClientAssignmentsAsync(cancellationToken);
	}

	public async Task<UserClientDetailsDTO> AssignUserClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken)
	{
		await GetAssignedAuthUserAsync(assignment.UserId, cancellationToken);
		var result = await _atsRepository.AssignUserClientAsync(assignment, cancellationToken);
		return result.Adapt<UserClientDetailsDTO>();
	}

	public Task<PaginatedResult<UserDetailsDTO>> GetUsersAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetUsers",
			Step = "FetchingUsers",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching users with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm)
			? _atsRepository.GetUsersAsync(paginationRequest, cancellationToken)
			: _atsRepository.SearchUsersAsync(paginationRequest, cancellationToken);
	}

	public Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
			throw new BadRequestException("Authenticated user ID is required.");

		return _atsRepository.GetActiveUserModuleIdsAsync(userId, cancellationToken);
	}

	public async Task<bool> AddUserAsync(
		IReadOnlyCollection<AddUserDTO> userDTOs,
		CancellationToken cancellationToken)
	{
		if (userDTOs.Count == 0)
			throw new BadRequestException("At least one module must be selected.");

		var user = userDTOs.First();
		if (userDTOs.Any(item => item.UserId != user.UserId))
			throw new BadRequestException("All module assignments must use the same Auth user.");

		var authUser = await GetAssignedAuthUserAsync(user.UserId, cancellationToken);
		var clientAssignment = await _atsRepository.GetUserClientAssignmentAsync(
			authUser.UserId,
			cancellationToken);
		var clientId = ResolveClientId(authUser, clientAssignment, user.ClientId);
		var users = userDTOs.Select(item => new AddUserDTO
		{
			UserId = authUser.UserId,
			UserName = authUser.UserName,
			UserEmail = authUser.UserEmail,
			IsActive = item.IsActive,
			ClientId = clientId,
			Site = item.Site,
			RoleId = item.RoleId,
			ModuleId = item.ModuleId
		}).ToArray();

		var logContext = new
		{
			Action = "AddUser",
			Step = "CreatingUser",
			UserEmail = authUser.UserEmail,
			ModuleCount = users.Select(item => item.ModuleId).Distinct().Count(),
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding user: {@Context}", logContext);
		return await _atsRepository.AddUserAsync(users, cancellationToken);
	}

	public async Task<IReadOnlyList<UserDetailsDTO>> EditUserAsync(
		IReadOnlyCollection<EditUserDTO> userDTOs,
		CancellationToken cancellationToken)
	{
		if (userDTOs.Count == 0)
			throw new BadRequestException("At least one module must be selected.");

		var user = userDTOs.First();
		if (userDTOs.Any(item => item.UserId != user.UserId))
			throw new BadRequestException("All module assignments must use the same Auth user.");

		var authUser = await GetAssignedAuthUserAsync(user.UserId, cancellationToken);
		var clientAssignment = await _atsRepository.GetUserClientAssignmentAsync(
			authUser.UserId,
			cancellationToken);
		var clientId = ResolveClientId(authUser, clientAssignment, user.ClientId);
		var users = userDTOs.Select(item => new EditUserDTO
		{
			UserId = authUser.UserId,
			UserName = authUser.UserName,
			UserEmail = authUser.UserEmail,
			IsActive = item.IsActive,
			ClientId = clientId,
			Site = item.Site,
			RoleId = item.RoleId,
			ModuleId = item.ModuleId
		}).ToArray();

		var logContext = new
		{
			Action = "EditUser",
			Step = "SynchronizingUserModules",
			user.UserId,
			ModuleCount = users.Select(item => item.ModuleId).Distinct().Count(),
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Synchronizing user module assignments: {@Context}", logContext);

		var updatedUsers = await _atsRepository.EditUserAsync(users, cancellationToken);
		return updatedUsers.Adapt<IReadOnlyList<UserDetailsDTO>>();
	}

	private async Task<ATSUserLookupDTO> GetAssignedAuthUserAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
			throw new BadRequestException("An Auth user is required.");

		var authUsers = await _authQueries.GetATSAssignedUsersAsync(cancellationToken);
		return authUsers.FirstOrDefault(user => user.UserId == userId)
			?? throw new BadRequestException("The selected user is not an active Auth user assigned to ATS.");
	}

	private static int? ResolveClientId(
		ATSUserLookupDTO authUser,
		UserClientDetails? clientAssignment,
		int? requestedClientId)
	{
		if (clientAssignment is not null)
			return clientAssignment.ClientId;

		if (string.Equals(authUser.UserEmail, SuperAdminEmail, StringComparison.OrdinalIgnoreCase))
		{
			return requestedClientId is > 0 ? requestedClientId : null;
		}

		throw new BadRequestException("Assign a client to this Auth user before configuring ATS access.");
	}
}
