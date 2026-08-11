namespace FrontendWebassembly.Component.ATS;

public partial class AddRoleComponent
{
	private MudForm? AddRoleForm;

	[CascadingParameter] IMudDialogInstance? AddRoleDialog { get; set; }

	[Parameter]
	public AddATSRoleDTO Role { get; set; } = new() { IsActive = true };

	private int DescriptionLength => Role.RoleDescription?.Length ?? 0;

	void Cancel() => AddRoleDialog!.Cancel();

	private void ToggleStatus() => Role.IsActive = !Role.IsActive;

	async Task Submit()
	{
		await AddRoleForm!.ValidateAsync();
		if (AddRoleForm.IsValid)
		{
			AddRoleDialog!.Close(DialogResult.Ok(Role));
		}
	}
}
