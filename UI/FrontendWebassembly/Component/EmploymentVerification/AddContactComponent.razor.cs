namespace FrontendWebassembly.Component.EmploymentVerification;

public partial class AddContactComponent
{
	[CascadingParameter] private IMudDialogInstance? Dialog { get; set; }

	private MudForm? _form;
	private string _companyName = string.Empty;
	private string _emailAddress = string.Empty;
	private bool _isActive = true;

	/// <summary>
	/// Returns the DTO and closes; it never calls the service itself. The page owns the
	/// call and the snackbar, which is what lets the same dialog be reused from a
	/// different caller later.
	/// </summary>
	private async Task SubmitAsync()
	{
		if (_form is null)
		{
			return;
		}

		await _form.Validate();

		if (!_form.IsValid)
		{
			return;
		}

		// Trimmed here as well as server side so the value the user sees in the table
		// after saving matches what they typed. The server still normalises - it cannot
		// trust a client - and lower-cases the email for the unique index.
		var contact = new AddEmploymentVerificationContactDTO
		{
			CompanyName = _companyName.Trim(),
			EmailAddress = _emailAddress.Trim(),
			IsActive = _isActive
		};

		Dialog?.Close(DialogResult.Ok(contact));
	}

	private void Cancel() => Dialog?.Cancel();
}
