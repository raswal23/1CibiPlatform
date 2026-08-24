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
