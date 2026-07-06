namespace FrontendWebassembly.Component.UserManagement;

public partial class EditRoleComponent
{
	private MudForm? EditRoleForm;

	[CascadingParameter] 
	IMudDialogInstance? EditRoleDialog { get; set; }

	[Parameter] 
	public RolesDTO Role { get; set; } = new RolesDTO();

	private RolesDTO EditRole = new();
	protected override void OnParametersSet()
	{
		Role = new RolesDTO
		{
			roleId = Role.roleId,
			roleName = Role.roleName,
			Description = Role.Description
		};
	}
	void Cancel() => EditRoleDialog!.Cancel();

	async Task Submit()
	{
		await EditRoleForm!.ValidateAsync();
		if (EditRoleForm!.IsValid)
		{
			EditRoleDialog!.Close(DialogResult.Ok(EditRoleDialog));
		}
	}
}
