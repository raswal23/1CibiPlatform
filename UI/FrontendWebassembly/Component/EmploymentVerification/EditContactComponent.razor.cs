namespace FrontendWebassembly.Component.EmploymentVerification;

public partial class EditContactComponent
{
	[CascadingParameter] private IMudDialogInstance? Dialog { get; set; }

	[Parameter, EditorRequired]
	public EmploymentVerificationContactDTO Contact { get; set; } = new();

	private MudForm? _form;
	private string _companyName = string.Empty;
	private string _emailAddress = string.Empty;
	private bool _isActive = true;

	// Copied into local fields rather than bound straight to Contact: the table holds
	// the same instance, so editing in place would move the row under the user even if
	// they cancelled.
	protected override void OnInitialized()
	{
		_companyName = Contact.CompanyName;
		_emailAddress = Contact.EmailAddress;
		_isActive = Contact.IsActive;
	}

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

		var edited = new EditEmploymentVerificationContactDTO
		{
			Id = Contact.Id,
			CompanyName = _companyName.Trim(),
			EmailAddress = _emailAddress.Trim(),
			IsActive = _isActive
		};

		Dialog?.Close(DialogResult.Ok(edited));
	}

	private void Cancel() => Dialog?.Cancel();
}
