namespace FrontendWebassembly.Component.UserManagement;

public partial class EditApplicationComponent
{
	private MudForm? EditApplicationForm;

	[CascadingParameter] IMudDialogInstance? EditApplicationDialog { get; set; }

	[Parameter] public ApplicationsDTO Application { get; set; } = new ApplicationsDTO();

	void Cancel() => EditApplicationDialog!.Cancel();

	async Task Submit()
	{
		await EditApplicationForm!.ValidateAsync();
		if (EditApplicationForm!.IsValid)
		{
			EditApplicationDialog!.Close(DialogResult.Ok(Application));
		}
	}
}
