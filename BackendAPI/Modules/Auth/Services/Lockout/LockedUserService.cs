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

	public async Task<KeysetPaginatedResult<AuthAttempts>> GetLockedUsersAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetLockedUsers",
			Step = "FetchingLockedUsers",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching locked users with pagination: {@Context}", logContext);

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		Guid? afterUserId = Guid.TryParse(fields?[0], out var userId) ? userId : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _authRepository.GetLockedUsersPageAsync(paginationRequest.SearchTerm, afterUserId, pageSize + 1, cancellationToken);
		var (lockedUsers, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(lockedUsers[^1].UserId.ToString("D"))
			: null;
		long? totalCount = afterUserId is null
			? await _authRepository.CountLockedUsersAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<AuthAttempts>(lockedUsers, nextCursor, totalCount);
	}
}
