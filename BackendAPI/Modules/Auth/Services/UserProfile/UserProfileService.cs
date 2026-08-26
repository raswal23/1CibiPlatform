namespace Auth.Services;

/// <summary>
/// Self-service profile updates. The user id always comes from the authenticated
/// principal, never from the request body, so this endpoint cannot be used to
/// rename another account.
/// </summary>
public class UserProfileService : IUserProfileService
{
	private readonly IUserProfileRepository _userProfileRepository;
	private readonly ICurrentUser _currentUser;
	private readonly ILogger<UserProfileService> _logger;

	public UserProfileService(
		IUserProfileRepository userProfileRepository,
		ICurrentUser currentUser,
		ILogger<UserProfileService> logger)
	{
		_userProfileRepository = userProfileRepository;
		_currentUser = currentUser;
		_logger = logger;
	}

	public async Task<UserProfileDTO> GetMyProfileAsync(
		CancellationToken cancellationToken)
	{
		var userId = RequireUserId();

		var user = await _userProfileRepository.GetProfileAsync(userId, cancellationToken);

		if (user is null)
		{
			_logger.LogError(
				"Profile was not found for the authenticated user: {@Context}",
				new
				{
					Action = "GetMyProfile",
					Step = "Fetch",
					UserId = userId,
					Timestamp = DateTime.UtcNow
				});

			throw new NotFoundException("The authenticated user profile was not found.");
		}

		return MapToProfile(user);
	}

	public async Task<UserProfileDTO> UpdateMyProfileAsync(
		UpdateUserProfileDTO profile,
		CancellationToken cancellationToken)
	{
		var userId = RequireUserId();

		var logContext = new
		{
			Action = "UpdateMyProfile",
			Step = "FetchForUpdate",
			UserId = userId,
			Timestamp = DateTime.UtcNow
		};

		var user = await _userProfileRepository.GetProfileAsync(userId, cancellationToken);

		if (user is null)
		{
			_logger.LogError(
				"Profile was not found during the update operation: {@Context}",
				logContext);

			throw new NotFoundException("The authenticated user profile was not found.");
		}

		// First and last name are required by the validator; the fallbacks only
		// guard against a caller that bypasses the pipeline.
		user.FirstName = Normalize(profile.FirstName) ?? user.FirstName;
		user.LastName = Normalize(profile.LastName) ?? user.LastName;
		user.MiddleName = Normalize(profile.MiddleName);

		var updated = await _userProfileRepository.UpdateProfileAsync(user, cancellationToken);

		_logger.LogInformation(
			"Profile updated for the authenticated user: {@Context}",
			logContext with { Step = "Updated" });

		return MapToProfile(updated);
	}

	private Guid RequireUserId()
	{
		if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
		{
			throw new UnauthorizedException("The current user is not authenticated.");
		}

		return userId;
	}

	// A blank middle name is stored as null rather than an empty string so the
	// column keeps one representation of "no middle name".
	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static UserProfileDTO MapToProfile(Authusers user) =>
		new()
		{
			UserId = user.Id,
			Email = user.Email,
			FirstName = user.FirstName,
			MiddleName = user.MiddleName,
			LastName = user.LastName,
			FullName = BuildFullName(user.FirstName, user.MiddleName, user.LastName)
		};

	// Matches the fullName claim JWTService mints, so the greeting in the top bar
	// reads the same before and after a rename.
	private static string BuildFullName(string firstName, string? middleName, string lastName) =>
		string.Join(
			' ',
			new[] { firstName, middleName, lastName }
				.Where(part => !string.IsNullOrWhiteSpace(part)));
}
