namespace Auth.Services;

public class AppSubRoleService : IAppSubRoleService
{
	private readonly IAppSubRoleRepository _authRepository;
	private readonly IEmailService _emailService;
	private readonly ILogger<AppSubRoleService> _logger;

	public AppSubRoleService(IAppSubRoleRepository authRepository,
						[FromKeyedServices("auth")] IEmailService emailService,
					    ILogger<AppSubRoleService> logger)
	{
		_authRepository = authRepository;
		_emailService = emailService;
		_logger = logger;
	}

	public async Task<KeysetPaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetAppSubRoles",
			Step = "FetchingAppSubRoles",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching appsubrole with pagination: {@Context}", logContext);

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		int? afterAppRoleId = int.TryParse(fields?[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var appRoleId) ? appRoleId : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _authRepository.GetAppSubRolesPageAsync(paginationRequest.SearchTerm, afterAppRoleId, pageSize + 1, cancellationToken);
		var (appSubRoles, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(appSubRoles[^1].AppRoleId.ToString(CultureInfo.InvariantCulture))
			: null;
		long? totalCount = afterAppRoleId is null
			? await _authRepository.CountAppSubRolesAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<AppSubRolesDTO>(appSubRoles, nextCursor, totalCount);
	}

	public async Task<bool> DeleteAppSubRoleAsync(int AppSubRoleId)
	{
		var logContext = new
		{
			Action = "DeleteAppSubRole",
			Step = "FetchForDelete",
			AppSubRoleId,
			Timestamp = DateTime.UtcNow
		};

		var appSubRole = await _authRepository.GetAppSubRoleAsync(AppSubRoleId);
		if (appSubRole == null)
		{
			_logger.LogError("{AppSubRoleId} was not found during delete operation: {@Context}", AppSubRoleId, logContext);
			throw new NotFoundException($"AppSubRole with ID {AppSubRoleId} was not found.");
		}
		var isDeleted = await _authRepository.DeleteAppSubRoleAsync(appSubRole);
		return isDeleted;
	}

	public async Task<AppSubRoleDTO> EditAppSubRoleAsync(EditAppSubRoleDTO appSubRoleDTO)
	{
		var logContext = new
		{
			Action = "EditAppSubRole",
			Step = "FetchForUpdate",
			AppSubRoleId = appSubRoleDTO.AppSubRoleId,
			Timestamp = DateTime.UtcNow
		};

		var existingAppSubRole = await _authRepository.GetAppSubRoleAsync(appSubRoleDTO.AppSubRoleId);
		if (existingAppSubRole == null)
		{
			_logger.LogError("{AppSubRoleId} was not found during update operation: {@Context}", appSubRoleDTO.AppSubRoleId, logContext);
			throw new NotFoundException($"AppSubRole with ID {appSubRoleDTO.AppSubRoleId} was not found.");
		}

		existingAppSubRole.UserId = appSubRoleDTO.UserId;
		existingAppSubRole.AppId = appSubRoleDTO.AppId!;
		existingAppSubRole.Submenu = appSubRoleDTO.SubMenuId;
		existingAppSubRole.RoleId = appSubRoleDTO.RoleId;

		var application = await _authRepository.EditAppSubRoleAsync(existingAppSubRole);
		return application.Adapt<AppSubRoleDTO>();
	}

	public async Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
	{
		var isAdded = await _authRepository.AddAppSubRoleAsync(appSubRole);
		return isAdded;
	}

	public async Task<bool> SendToUserEmailAsync(AccountNotificationDTO accountNotificationDTO)
	{
		var logContext = new
		{
			Action = "SendEmailNotification",
			Step = "SendNotification",
			Email = accountNotificationDTO.Gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending notification for email: {@Context}", logContext);

		var otpBody = _emailService.SendNotificationBody(accountNotificationDTO.Gmail!, accountNotificationDTO.Application!, accountNotificationDTO.SubMenu!, accountNotificationDTO.Role!);

		var isSent = await _emailService.SendEmailAsync(
			toEmail: accountNotificationDTO.Gmail!,
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
