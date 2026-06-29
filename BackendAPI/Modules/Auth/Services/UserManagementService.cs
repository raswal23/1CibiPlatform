namespace Auth.Services;

public class UserManagementService : IUserService
{
	private readonly IAuthRepository _authRepository;
	private readonly IEmailService _emailService;
	private readonly ILogger<UserManagementService> _logger;

	public UserManagementService(IAuthRepository authRepository,
					   [FromKeyedServices("auth")] IEmailService emailService,
					   ILogger<UserManagementService> logger)
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

	public Task<PaginatedResult<UsersDTO>> GetUsersAsync(
		PaginationRequest paginationRequest,
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

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_authRepository.GetUserAsync(paginationRequest, cancellationToken) :
			_authRepository.SearchUserAsync(paginationRequest, cancellationToken);
	}

	public Task<PaginatedResult<UsersDTO>> GetUnApprovedUsersAsync(
		PaginationRequest paginationRequest,
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

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_authRepository.GetUnapprovedUserAsync(paginationRequest, cancellationToken) :
			_authRepository.SearchUnApprovedUserAsync(paginationRequest, cancellationToken);
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
