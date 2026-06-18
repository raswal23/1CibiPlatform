namespace FrontendWebassembly.Component.UserManagement;

public partial class EditRoleComponent
{
	private MudForm? EditRoleForm;

	[CascadingParameter] IMudDialogInstance? EditRoleDialog { get; set; }

	[Parameter] public RolesDTO Role { get; set; } = new RolesDTO();

	void Cancel() => EditRoleDialog!.Cancel();

	async Task Submit()
	{
		await EditRoleForm!.ValidateAsync();
		if (EditRoleForm!.IsValid)
		{
			EditRoleDialog!.Close(DialogResult.Ok(Role));
		}
	}
}
