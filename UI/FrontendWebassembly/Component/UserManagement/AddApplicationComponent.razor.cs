namespace FrontendWebassembly.Component.UserManagement;

public partial class AddApplicationComponent
{
	private MudForm? AddApplicationForm;

	[CascadingParameter] IMudDialogInstance? AddApplicationDialog { get; set; }

	[Parameter]
	public AddApplicationDTO Application { get; set; } = new AddApplicationDTO { IsActive = true };

	void Cancel() => AddApplicationDialog!.Cancel();

	async Task Submit()
	{
		await AddApplicationForm!.ValidateAsync();
		if (AddApplicationForm!.IsValid)
		{
			AddApplicationDialog!.Close(DialogResult.Ok(Application));
		}
	}
}
