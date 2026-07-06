namespace FrontendWebassembly.Component.UserManagement;

public partial class EditApplicationComponent
{
	private MudForm? EditApplicationForm;

	[CascadingParameter]
	IMudDialogInstance? EditApplicationDialog { get; set; }

	[Parameter]
	public ApplicationsDTO Application { get; set; } = new();

	private ApplicationsDTO EditApplication = new();

	protected override void OnParametersSet()
	{
		EditApplication = new ApplicationsDTO
		{
			applicationId = Application.applicationId,
			applicationName = Application.applicationName,
			Description = Application.Description,
			IsActive = Application.IsActive
		};
	}

	void Cancel() => EditApplicationDialog!.Cancel();

	async Task Submit()
	{
		await EditApplicationForm!.ValidateAsync();
		if (EditApplicationForm!.IsValid)
		{
			EditApplicationDialog!.Close(DialogResult.Ok(EditApplication));
		}
	}
}
