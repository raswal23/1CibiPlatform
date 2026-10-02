namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// Collects a new sender mailbox. Closes with the DTO; the page does the posting.
/// </summary>
/// <remarks>
/// Deliberately does not call the service itself, matching <see cref="AddPackageComponent"/>:
/// the dialog's job ends at a valid DTO, and the page owns the register-then-verify sequence
/// because it is the thing that has to open the code dialog next and reload the table after.
/// </remarks>
public partial class AddEmailAccountComponent
{
	private MudForm? AddEmailAccountForm;

	[Inject] private EmailValidationService EmailValidation { get; set; } = default!;

	[CascadingParameter] private IMudDialogInstance? AddEmailAccountDialog { get; set; }

	[Parameter] public RegisterEmailAccountDTO Account { get; set; } = new();

	private bool showPassword;

	private string? ValidateEmail(string value) => EmailValidation.ValidateEmail(value);

	private void TogglePasswordVisibility() => showPassword = !showPassword;

	private void ToggleStatus() => Account.IsActive = !Account.IsActive;

	private void Cancel() => AddEmailAccountDialog!.Cancel();

	private async Task Submit()
	{
		await AddEmailAccountForm!.ValidateAsync();

		if (AddEmailAccountForm!.IsValid)
		{
			AddEmailAccountDialog!.Close(DialogResult.Ok(Account));
		}
	}
}
