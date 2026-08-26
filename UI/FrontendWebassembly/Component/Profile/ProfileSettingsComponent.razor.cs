namespace FrontendWebassembly.Component.Profile;

public partial class ProfileSettingsComponent
{
	private MudForm? ProfileForm;

	[CascadingParameter]
	private IMudDialogInstance? ProfileDialog { get; set; }

	[Inject]
	private IUserProfileService UserProfileService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Parameter]
	public UserProfileDTO Profile { get; set; } = new();

	private UpdateUserProfileDTO EditProfile { get; set; } = new();

	private bool IsSubmitting { get; set; }

	private string PreviewName => BuildFullName(
		EditProfile.FirstName,
		EditProfile.MiddleName,
		EditProfile.LastName) is { Length: > 0 } name
		? name
		: "Your name";

	private string Initials
	{
		get
		{
			var parts = new[] { EditProfile.FirstName, EditProfile.LastName }
				.Where(part => !string.IsNullOrWhiteSpace(part))
				.Select(part => char.ToUpperInvariant(part!.Trim()[0]));

			var value = string.Concat(parts);

			return string.IsNullOrEmpty(value) ? "U" : value;
		}
	}

	// The save button stays disabled until something actually differs, so a
	// no-op dialog cannot fire a needless request.
	private bool HasChanges =>
		!string.Equals(Normalize(EditProfile.FirstName), Normalize(Profile.FirstName), StringComparison.Ordinal) ||
		!string.Equals(Normalize(EditProfile.MiddleName), Normalize(Profile.MiddleName), StringComparison.Ordinal) ||
		!string.Equals(Normalize(EditProfile.LastName), Normalize(Profile.LastName), StringComparison.Ordinal);

	protected override void OnParametersSet()
	{
		EditProfile = new UpdateUserProfileDTO
		{
			FirstName = Profile.FirstName,
			MiddleName = Profile.MiddleName,
			LastName = Profile.LastName
		};
	}

	private void Cancel() => ProfileDialog!.Cancel();

	private async Task Submit()
	{
		if (IsSubmitting)
			return;

		await ProfileForm!.ValidateAsync();

		if (!ProfileForm!.IsValid)
			return;

		IsSubmitting = true;

		try
		{
			var payload = new UpdateUserProfileDTO
			{
				FirstName = Normalize(EditProfile.FirstName),
				MiddleName = Normalize(EditProfile.MiddleName),
				LastName = Normalize(EditProfile.LastName)
			};

			var response = await UserProfileService.UpdateMyProfileAsync(payload);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(
					string.IsNullOrWhiteSpace(response.ErrorDetail)
						? "Your profile could not be updated."
						: response.ErrorDetail,
					Severity.Error);

				return;
			}

			Snackbar.Add("Profile updated successfully", Severity.Success);
			ProfileDialog!.Close(DialogResult.Ok(response.Data));
		}
		finally
		{
			IsSubmitting = false;
		}
	}

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

	private static string BuildFullName(string? firstName, string? middleName, string? lastName) =>
		string.Join(
			' ',
			new[] { firstName, middleName, lastName }
				.Where(part => !string.IsNullOrWhiteSpace(part))
				.Select(part => part!.Trim()));
}
