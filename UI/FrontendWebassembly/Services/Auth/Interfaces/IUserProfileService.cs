namespace FrontendWebassembly.Services.Auth.Interfaces;

public interface IUserProfileService
{
	Task<ServiceResponse<UserProfileDTO>> GetMyProfileAsync(CancellationToken cancellationToken = default);

	Task<ServiceResponse<UserProfileDTO>> UpdateMyProfileAsync(
		UpdateUserProfileDTO profile,
		CancellationToken cancellationToken = default);
}
