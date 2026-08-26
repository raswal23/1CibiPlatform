namespace Auth.Features.UserProfile.Command.UpdateMyProfile;

public record UpdateMyProfileCommand(UpdateUserProfileDTO updateProfile) : ICommand<UpdateMyProfileResult>;

public record UpdateMyProfileResult(UserProfileDTO Profile);

public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
	// Lengths mirror AuthusersConfiguration so invalid input fails validation with
	// a 400 instead of reaching PostgreSQL as a DbUpdateException.
	public UpdateMyProfileCommandValidator()
	{
		RuleFor(x => x.updateProfile)
			.NotNull().WithMessage("Profile data is required.");

		When(x => x.updateProfile != null, () =>
		{
			RuleFor(x => x.updateProfile.FirstName)
				.NotEmpty().WithMessage("First name is required.")
				.MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

			RuleFor(x => x.updateProfile.LastName)
				.NotEmpty().WithMessage("Last name is required.")
				.MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

			RuleFor(x => x.updateProfile.MiddleName)
				.MaximumLength(100).WithMessage("Middle name must not exceed 100 characters.")
				.When(x => !string.IsNullOrWhiteSpace(x.updateProfile.MiddleName));
		});
	}
}

public class UpdateMyProfileHandler : ICommandHandler<UpdateMyProfileCommand, UpdateMyProfileResult>
{
	private readonly IUserProfileService _userProfileService;

	public UpdateMyProfileHandler(IUserProfileService userProfileService)
	{
		_userProfileService = userProfileService;
	}

	public async Task<UpdateMyProfileResult> Handle(
		UpdateMyProfileCommand request,
		CancellationToken cancellationToken)
	{
		var profile = await _userProfileService.UpdateMyProfileAsync(
			request.updateProfile,
			cancellationToken);

		return new UpdateMyProfileResult(profile);
	}
}
