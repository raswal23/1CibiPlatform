namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// Edits a registered sender mailbox. Closes with the DTO; the page does the posting.
/// </summary>
/// <remarks>
/// The credential fields are bound through explicit handlers rather than <c>@bind-Value</c> so
/// the dialog can tell, while the operator types, whether this edit will need a code. That
/// prediction is a courtesy only - the server decides on its own by comparing against the stored
/// row, and its answer is what the page acts on.
/// </remarks>
public partial class EditEmailAccountComponent
{
	private MudForm? EditEmailAccountForm;

	[Inject] private EmailValidationService EmailValidation { get; set; } = default!;

	[CascadingParameter] private IMudDialogInstance? EditEmailAccountDialog { get; set; }

	[Parameter] public EmailAccountDTO Account { get; set; } = new();

	private EditEmailAccountDTO EditAccount = new();
	private bool showPassword;

	// Snapshotted on open so the comparison is against what was loaded, not against whatever
	// the field happens to hold after the operator has been typing.
	private string originalEmailAddress = string.Empty;
	private string originalSmtpHost = string.Empty;
	private int originalSmtpPort;

	private string PasswordPlaceholder =>
		Account.HasPassword ? "Leave blank to keep the stored password" : "Enter an app password";

	/// <summary>
	/// True when this edit touches a credential field, which is what makes a code necessary.
	/// </summary>
	private bool RequiresVerification =>
		!string.IsNullOrEmpty(EditAccount.AppPassword)
		|| !string.Equals(EditAccount.EmailAddress, originalEmailAddress, StringComparison.OrdinalIgnoreCase)
		|| !string.Equals(EditAccount.SmtpHost, originalSmtpHost, StringComparison.OrdinalIgnoreCase)
		|| EditAccount.SmtpPort != originalSmtpPort;

	protected override void OnParametersSet()
	{
		EditAccount = new EditEmailAccountDTO
		{
			AtsEmailAccountId = Account.AtsEmailAccountId,
			DisplayName = Account.DisplayName,
			EmailAddress = Account.EmailAddress,
			SmtpHost = Account.SmtpHost,
			SmtpPort = Account.SmtpPort,

			// Blank, always. The stored password never reaches the browser, so there is nothing
			// to pre-fill and a blank field is exactly what "keep it" posts.
			AppPassword = null,
			Priority = Account.Priority,
			DailySendLimit = Account.DailySendLimit,
			IsActive = Account.IsActive
		};

		originalEmailAddress = Account.EmailAddress;
		originalSmtpHost = Account.SmtpHost;
		originalSmtpPort = Account.SmtpPort;
	}

	private string? ValidateEmail(string value) => EmailValidation.ValidateEmail(value);

	private void OnEmailChanged(string value) => EditAccount.EmailAddress = value;

	private void OnHostChanged(string value) => EditAccount.SmtpHost = value;

	private void OnPortChanged(int value) => EditAccount.SmtpPort = value;

	// Normalises whitespace-only input to null so a stray space in the box does not read as a
	// new password and force a pointless re-verification.
	private void OnPasswordChanged(string? value) =>
		EditAccount.AppPassword = string.IsNullOrWhiteSpace(value) ? null : value;

	private void TogglePasswordVisibility() => showPassword = !showPassword;

	private void ToggleStatus() => EditAccount.IsActive = !EditAccount.IsActive;

	private void Cancel() => EditEmailAccountDialog!.Cancel();

	private async Task Submit()
	{
		await EditEmailAccountForm!.ValidateAsync();

		if (EditEmailAccountForm!.IsValid)
		{
			EditEmailAccountDialog!.Close(DialogResult.Ok(EditAccount));
		}
	}
}
