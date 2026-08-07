namespace FrontendWebassembly.Component.ATS;

public partial class AddRoleComponent
{
	private MudForm? AddRoleForm;

	[CascadingParameter] IMudDialogInstance? AddRoleDialog { get; set; }

	[Parameter]
	public AddATSRoleDTO Role { get; set; } = new() { IsActive = true };

	void Cancel() => AddRoleDialog!.Cancel();

	async Task Submit()
	{
		await AddRoleForm!.ValidateAsync();
		if (AddRoleForm.IsValid)
		{
			AddRoleDialog!.Close(DialogResult.Ok(Role));
		}
	}
}
