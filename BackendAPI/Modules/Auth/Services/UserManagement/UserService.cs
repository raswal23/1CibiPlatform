namespace Auth.Services;

public class UserService : IUserService
{
	private readonly IUserRepository _authRepository;
	private readonly IEmailService _emailService;
	private readonly ILogger<UserService> _logger;

	public UserService(IUserRepository authRepository,
					   [FromKeyedServices("auth")] IEmailService emailService,
					   ILogger<UserService> logger)
	{
		_authRepository = authRepository;
		_emailService = emailService;
		_logger = logger;
	}

	public async Task<UserDTO> EditUserAsync(EditUserDTO userDTO)
	{
		var logContext = new
		{
			Action = "EditUser",
			Step = "FetchForUpdate",
			userDTO.Email,
			Timestamp = DateTime.UtcNow
		};

		var existingUser = await _authRepository.GetUserAsync(userDTO.Email!);
		if (existingUser == null)
		{
			_logger.LogError("{Email} was not found during update operation: {@Context}", userDTO.Email, logContext);
			throw new NotFoundException($"{userDTO.Email} was not found.");
		}

		existingUser.IsApproved = userDTO.IsApproved;

		var user = await _authRepository.EditUserAsync(existingUser);
		return user.Adapt<UserDTO>();
	}

	/// <summary>
	/// Activates or deactivates a user account.
	/// </summary>
	/// <remarks>
	/// Loads through GetUserByIdAsync rather than GetRawUserAsync, which filters on
	/// IsActive - reactivating an inactive user is the whole point, and the filtered lookup
	/// would answer 404 for exactly that case.
	///
	/// Only IsActive is written. Approval belongs to the approval queue, and name/email
	/// belong to the user, so a status edit that could reach either would be a different
	/// operation wearing this one's name.
	///
	/// Saved through EditUserAsync deliberately: that is the write the cache decorator hangs
	/// its UsersTag/UnApprovedUsersTag invalidation off, so the board reflects the change
	/// immediately rather than serving a cached first page.
	/// </remarks>
	public async Task<UserDTO> EditUserStatusAsync(EditUserStatusDTO userStatusDTO)
	{
		var logContext = new
		{
			Action = "EditUserStatus",
			Step = "FetchForUpdate",
			userStatusDTO.UserId,
			userStatusDTO.IsActive,
			Timestamp = DateTime.UtcNow
		};

		var existingUser = await _authRepository.GetUserByIdAsync(userStatusDTO.UserId);
		if (existingUser == null)
		{
			_logger.LogError("User {UserId} was not found during status update: {@Context}", userStatusDTO.UserId, logContext);
			throw new NotFoundException($"User {userStatusDTO.UserId} was not found.");
		}

		existingUser.IsActive = userStatusDTO.IsActive;

		var user = await _authRepository.EditUserAsync(existingUser);

		_logger.LogInformation("User {UserId} status set to {IsActive}: {@Context}", userStatusDTO.UserId, userStatusDTO.IsActive, logContext);

		return user.Adapt<UserDTO>();
	}

	/// <summary>
	/// Rejects a user awaiting approval by deactivating the account.
	/// </summary>
	/// <remarks>
	/// Deactivates rather than deletes. IsActive is what both list queries already filter on
	/// (BuildUsersQuery and BuildUnapprovedUsersQuery), so clearing it takes the row off the
	/// approval queue without destroying the registration - a rejection that turns out to be
	/// wrong is then a data fix rather than a re-registration. It also keeps the login refusal
	/// intact: LoginService still finds the account and IsApproved is still false.
	///
	/// Routed through EditUserAsync deliberately - that is the write the cache decorator hangs
	/// its UsersTag and UnApprovedUsersTag invalidation off, so the approval tab reflects this
	/// immediately rather than serving a cached first page.
	/// </remarks>
	public async Task<bool> RejectUserAsync(Guid userId)
	{
		var logContext = new
		{
			Action = "RejectUser",
			Step = "FetchForUpdate",
			UserId = userId,
			Timestamp = DateTime.UtcNow
		};

		// GetRawUserAsync already filters on IsActive, so a second rejection of the same
		// user finds nothing and is reported as not-found rather than silently succeeding.
		var existingUser = await _authRepository.GetRawUserAsync(userId);
		if (existingUser == null)
		{
			_logger.LogError("User {UserId} was not found during reject operation: {@Context}", userId, logContext);
			throw new NotFoundException($"User {userId} was not found.");
		}

		// Guard against rejecting someone already approved: this is the approval queue's
		// action, and an approved user leaving through it would be an account deactivation
		// wearing the wrong name.
		if (existingUser.IsApproved)
		{
			_logger.LogWarning("User {UserId} is already approved and cannot be rejected: {@Context}", userId, logContext);
			throw new BadRequestException("This user has already been approved.");
		}

		existingUser.IsActive = false;

		await _authRepository.EditUserAsync(existingUser);

		_logger.LogInformation("User {UserId} was rejected: {@Context}", userId, logContext);

		return true;
	}

	public async Task<KeysetPaginatedResult<UsersDTO>> GetUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetUsers",
			Step = "StartFetching",
			PaginationRequest = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching users with pagination: {@Context}", logContext);

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		Guid? afterId = Guid.TryParse(fields?[0], out var userId) ? userId : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _authRepository.GetUsersPageAsync(paginationRequest.SearchTerm, afterId, pageSize + 1, cancellationToken);
		var (users, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(users[^1].userId.ToString("D"))
			: null;
		long? totalCount = afterId is null
			? await _authRepository.CountUsersAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<UsersDTO>(users, nextCursor, totalCount);
	}

	public async Task<KeysetPaginatedResult<UsersDTO>> GetUnApprovedUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetUnApprovedUser",
			Step = "StartFetching",
			PaginationRequest = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching unapproved users with pagination: {@Context}", logContext);

		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		Guid? afterId = Guid.TryParse(fields?[0], out var userId) ? userId : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _authRepository.GetUnapprovedUsersPageAsync(paginationRequest.SearchTerm, afterId, pageSize + 1, cancellationToken);
		var (users, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(users[^1].userId.ToString("D"))
			: null;
		long? totalCount = afterId is null
			? await _authRepository.CountUnapprovedUsersAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<UsersDTO>(users, nextCursor, totalCount);
	}

	public async Task<bool> SendApprovalToUserEmailAsync(string Gmail)
	{
		var logContext = new
		{
			Action = "SendEmailNotification",
			Step = "SendNotification",
			Email = Gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending notification for email: {@Context}", logContext);

		var otpBody = _emailService.SendApprovalNotificationBody(Gmail!);

		var isSent = await _emailService.SendEmailAsync(
			toEmail: Gmail!,
			subject: "Account Assignment Notification",
			body: otpBody
		);

		if (!isSent)
		{
			_logger.LogError("Failed to send Notification email to: {@Context}", logContext);
			throw new InternalServerException("Failed to send Notification email.");
		}

		return isSent;
	}
}
