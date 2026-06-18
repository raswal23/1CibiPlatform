namespace FrontendWebassembly.Component.UserManagement;

public partial class AddRoleComponent
{
	private MudForm? AddRoleForm;

	[CascadingParameter] IMudDialogInstance? MudDialog { get; set; }

	[Parameter]
	public AddRoleDTO Role { get; set; } = new AddRoleDTO();

	void Cancel() => MudDialog!.Cancel();

	async Task Submit()
	{
		await AddRoleForm!.ValidateAsync();
		if (AddRoleForm.IsValid)
		{
			MudDialog!.Close(DialogResult.Ok(Role));
		}
	}
}
