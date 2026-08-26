namespace Auth.Services;

public interface IUserProfileService
{
	Task<UserProfileDTO> GetMyProfileAsync(
		CancellationToken cancellationToken);

	Task<UserProfileDTO> UpdateMyProfileAsync(
		UpdateUserProfileDTO profile,
		CancellationToken cancellationToken);
}
