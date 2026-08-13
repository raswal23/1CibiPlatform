namespace Auth.Services;

public class LockedUserService : ILockerUserService
{
	private readonly ILockoutRepository _authRepository;
	private readonly ILogger<LockedUserService> _logger;
	public LockedUserService(ILockoutRepository authRepository,
							 ILogger<LockedUserService> logger)
	{
		_authRepository = authRepository;
		_logger = logger;
	}
	public async Task<bool> DeleteLockedUserAsync(Guid lockedUserId)
	{
		var logContext = new
		{
			Action = "DeleteLockedUser",
			Step = "FetchForDelete",
			lockedUserId,
			Timestamp = DateTime.UtcNow
		};

		var lockedUser = await _authRepository.GetLockedUserAsync(lockedUserId);

		if (lockedUser == null)
		{
			_logger.LogError("{LockedUser} was not found during delete operation: {@Context}", lockedUserId, logContext);
			throw new NotFoundException($"Locked user with ID {lockedUserId} was not found.");
		}

		var isDeleted = await _authRepository.DeleteLockedUserAsync(lockedUser);
		return isDeleted;
	}

	public Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetLockedUsers",
			Step = "FetchingLockedUsers",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching locked users with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_authRepository.GetLockedUsersAsync(paginationRequest, cancellationToken) :
			_authRepository.SearchLockedUserAsync(paginationRequest, cancellationToken);
	}
}
