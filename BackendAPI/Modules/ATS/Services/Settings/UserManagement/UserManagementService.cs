namespace ATS.Services.Settings.UserManagement;

public class UserManagementService : IUserManagementService
{
	private readonly IATSUserRepository _userRepository;
	private readonly IUserClientRepository _userClientRepository;
	private readonly IAuthQueries _authQueries;
	private readonly ICurrentUser _currentUser;
	private readonly ILogger<UserManagementService> _logger;

	public UserManagementService(
		IATSUserRepository userRepository,
		IUserClientRepository userClientRepository,
		IAuthQueries authQueries,
		ICurrentUser currentUser,
		ILogger<UserManagementService> logger)
	{
		_userRepository = userRepository;
		_userClientRepository = userClientRepository;
		_authQueries = authQueries;
		_currentUser = currentUser;
		_logger = logger;
	}

	public async Task<IReadOnlyList<ATSUserLookupDTO>> GetAuthUsersAsync(
		CancellationToken cancellationToken)
	{
		var scope = ResolveScope();
		if (scope.IsDenied)
			return Array.Empty<ATSUserLookupDTO>();

		var users = await _authQueries.GetATSAssignedUsersAsync(cancellationToken);
		if (scope.CanAccessAll)
			return users;

		var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
			scope.ClientId!.Value,
			cancellationToken);
		var allowedUserIds = assignments.Select(item => item.UserId).ToHashSet();
		return users.Where(user => allowedUserIds.Contains(user.UserId)).ToArray();
	}

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken)
	{
		var scope = ResolveScope();
		if (scope.IsDenied)
			return Array.Empty<UserClientDetailsDTO>();

		return scope.CanAccessAll
			? await _userClientRepository.GetUserClientAssignmentsAsync(cancellationToken)
			: await _userClientRepository.GetUserClientAssignmentsAsync(scope.ClientId!.Value, cancellationToken);
	}

	public async Task<UserClientDetailsDTO> AssignUserClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken)
	{
		var scope = RequireWriteScope();
		if (!scope.CanAccessAll)
		{
			if (assignment.ClientId != scope.ClientId)
				throw new ForbiddenException("The requested client is outside the current ATS scope.");

			var existingAssignment = await _userClientRepository.GetUserClientAssignmentAsync(
				assignment.UserId,
				cancellationToken);
			if (existingAssignment is not null && existingAssignment.ClientId != scope.ClientId)
				throw new ForbiddenException("The selected user belongs to another ATS client.");
		}

		await GetAssignedAuthUserAsync(assignment.UserId, cancellationToken);
		if (!await _userClientRepository.ClientIsActiveAsync(assignment.ClientId, cancellationToken))
			throw new BadRequestException("The selected client does not exist or is inactive.");

		var result = await _userClientRepository.AssignUserClientAsync(assignment, cancellationToken);
		return result.Adapt<UserClientDetailsDTO>();
	}

	public async Task<KeysetPaginatedResult<UserDetailsDTO>> GetUsersAsync(
		KeysetPaginationRequest paginationRequest,
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

		var scope = ResolveScope();
		if (scope.IsDenied)
			return new KeysetPaginatedResult<UserDetailsDTO>(Array.Empty<UserDetailsDTO>(), null, 0);

		var clientId = scope.CanAccessAll ? null : scope.ClientId;

		// Keyset over the grouped (UserName, UserEmail, UserId) keys; the page items are
		// re-fetched by id so each logical user expands to one row per module. An
		// undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 3);
		Guid? afterUserId = Guid.TryParse(fields?[2], out var parsedUserId) ? parsedUserId : null;
		var (afterUserName, afterUserEmail) = afterUserId.HasValue ? (fields![0], fields[1]) : (null, null);
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var keys = await _userRepository.GetUserPageKeysAsync(
			paginationRequest.SearchTerm, clientId, afterUserName, afterUserEmail,
			afterUserName is null ? null : afterUserId, pageSize + 1, cancellationToken);
		var (pageKeys, hasMore) = KeysetPage.Trim(keys, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(pageKeys[^1].UserName, pageKeys[^1].UserEmail, pageKeys[^1].UserId.ToString("D"))
			: null;
		long? totalCount = afterUserName is null
			? await _userRepository.CountUsersAsync(paginationRequest.SearchTerm, clientId, cancellationToken)
			: null;

		var items = pageKeys.Count == 0
			? []
			: await _userRepository.GetUsersByIdsAsync(
				pageKeys.Select(key => key.UserId).ToList(), paginationRequest.SearchTerm, clientId, cancellationToken);

		return new KeysetPaginatedResult<UserDetailsDTO>(items, nextCursor, totalCount);
	}

	public async Task<int?> GetCurrentUserRoleIdAsync(CancellationToken cancellationToken)
	{
		if (_currentUser.UserId is not Guid userId || userId == Guid.Empty)
			throw new UnauthorizedAccessException("An authenticated user is required.");

		var roleIds = await _userRepository.GetActiveUserRoleIdsAsync(userId, cancellationToken);
		if (roleIds.Count == 1)
			return roleIds[0];

		if (roleIds.Count > 1)
		{
			_logger.LogWarning(
				"ATS role lookup returned inconsistent roles for user {UserId}: {RoleIds}",
				userId,
				roleIds);
		}

		return null;
	}

	public Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
			throw new BadRequestException("Authenticated user ID is required.");

		return _userRepository.GetActiveUserModuleIdsAsync(userId, cancellationToken);
	}

	public async Task<bool> AddUserAsync(
		IReadOnlyCollection<AddUserDTO> userDTOs,
		CancellationToken cancellationToken)
	{
		var scope = RequireWriteScope();
		if (userDTOs.Count == 0)
			throw new BadRequestException("At least one module must be selected.");

		var user = userDTOs.First();
		if (userDTOs.Any(item => item.UserId != user.UserId))
			throw new BadRequestException("All module assignments must use the same Auth user.");

		var authUser = await GetAssignedAuthUserAsync(user.UserId, cancellationToken);
		var clientAssignment = await _userClientRepository.GetUserClientAssignmentAsync(
			authUser.UserId,
			cancellationToken);
		var clientId = ResolveClientId(
			clientAssignment,
			user.ClientId,
			_currentUser.IsPlatformSuperAdmin);
		EnsureClientAccess(scope, clientId);

		var email = authUser.UserEmail.Trim();
		if (await _userRepository.UserExistsAsync(authUser.UserId, email, cancellationToken))
			throw new BadRequestException("The selected Auth user already exists in ATS User Management.");
		if (clientId.HasValue && !await _userClientRepository.ClientIsActiveAsync(clientId.Value, cancellationToken))
			throw new BadRequestException("The selected client does not exist or is inactive.");
		if (!await _userRepository.RoleIsActiveAsync(user.RoleId, cancellationToken))
			throw new BadRequestException("The selected role does not exist or is inactive.");

		var moduleIds = userDTOs.Select(item => item.ModuleId).Distinct().ToArray();
		if (await _userRepository.CountActiveModulesAsync(moduleIds, cancellationToken) != moduleIds.Length)
			throw new BadRequestException("One or more selected modules do not exist or are inactive.");

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
		return await _userRepository.AddUserAsync(users, cancellationToken);
	}

	public async Task<IReadOnlyList<UserDetailsDTO>> EditUserAsync(
		IReadOnlyCollection<EditUserDTO> userDTOs,
		CancellationToken cancellationToken)
	{
		var scope = RequireWriteScope();
		if (userDTOs.Count == 0)
			throw new BadRequestException("At least one module must be selected.");

		var user = userDTOs.First();
		if (userDTOs.Any(item => item.UserId != user.UserId))
			throw new BadRequestException("All module assignments must use the same Auth user.");

		var existingUsers = await _userRepository.GetUserAsync(user.UserId, cancellationToken);
		if (!scope.CanAccessAll && (existingUsers.Count == 0 || existingUsers.Any(item => item.ClientId != scope.ClientId)))
			throw new ForbiddenException("The selected user is outside the current ATS scope.");
		if (existingUsers.Count == 0)
			throw new NotFoundException($"User with ID {user.UserId} was not found.");

		var authUser = await GetAssignedAuthUserAsync(user.UserId, cancellationToken);
		var clientAssignment = await _userClientRepository.GetUserClientAssignmentAsync(
			authUser.UserId,
			cancellationToken);
		var clientId = ResolveClientId(
			clientAssignment,
			user.ClientId,
			_currentUser.IsPlatformSuperAdmin);
		EnsureClientAccess(scope, clientId);

		var email = authUser.UserEmail.Trim();
		if (await _userRepository.UserEmailExistsAsync(authUser.UserId, email, cancellationToken))
			throw new BadRequestException($"User with email '{email}' already exists.");
		if (existingUsers[0].ClientId != clientId && clientId.HasValue && !await _userClientRepository.ClientIsActiveAsync(clientId.Value, cancellationToken))
			throw new BadRequestException("The selected client does not exist or is inactive.");
		if (existingUsers[0].RoleId != user.RoleId && !await _userRepository.RoleIsActiveAsync(user.RoleId, cancellationToken))
			throw new BadRequestException("The selected role does not exist or is inactive.");

		var newModuleIds = userDTOs.Select(item => item.ModuleId).Distinct()
			.Except(existingUsers.Select(item => item.ModuleId)).ToArray();
		if (newModuleIds.Length > 0 && await _userRepository.CountActiveModulesAsync(newModuleIds, cancellationToken) != newModuleIds.Length)
			throw new BadRequestException("One or more newly selected modules do not exist or are inactive.");

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

		var updatedUsers = await _userRepository.EditUserAsync(users, cancellationToken);
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
		UserClientDetails? clientAssignment,
		int? requestedClientId,
		bool allowWithoutClientAssignment)
	{
		if (clientAssignment is not null)
			return clientAssignment.ClientId;

		if (allowWithoutClientAssignment)
			return requestedClientId is > 0 ? requestedClientId : null;

		throw new BadRequestException("Assign a client to this Auth user before configuring ATS access.");
	}

	private UserManagementScope ResolveScope()
	{
		if (_currentUser.IsPlatformSuperAdmin)
			return new UserManagementScope(true, null);

		return _currentUser.AtsRoleId switch
		{
			AtsRoleIds.PlatformManager => new UserManagementScope(true, null),
			AtsRoleIds.Admin when _currentUser.AtsClientId is > 0 =>
				new UserManagementScope(false, _currentUser.AtsClientId),
			_ => UserManagementScope.Denied
		};
	}

	private UserManagementScope RequireWriteScope()
	{
		var scope = ResolveScope();
		if (scope.IsDenied)
			throw new ForbiddenException("The current user does not have ATS user-management access.");

		return scope;
	}

	private static void EnsureClientAccess(UserManagementScope scope, int? targetClientId)
	{
		if (!scope.CanAccessAll && targetClientId != scope.ClientId)
			throw new ForbiddenException("The selected user is outside the current ATS scope.");
	}

	private readonly record struct UserManagementScope(bool CanAccessAll, int? ClientId)
	{
		public static UserManagementScope Denied => new(false, null);
		public bool IsDenied => !CanAccessAll && !ClientId.HasValue;
	}
}
