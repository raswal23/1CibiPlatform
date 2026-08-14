namespace FrontendWebassembly.Component.ATS;

public partial class EditRoleComponent
{
	private MudForm? EditRoleForm;

	[CascadingParameter]
	IMudDialogInstance? EditRoleDialog { get; set; }

	[Parameter]
	public RoleDetailsDTO Role { get; set; } = new();

	private EditATSRoleDTO EditRole = new();

	private int DescriptionLength => EditRole.RoleDescription?.Length ?? 0;

	protected override void OnParametersSet()
	{
		EditRole = new EditATSRoleDTO
		{
			RoleId = Role.RoleId,
			RoleName = Role.RoleName,
			RoleDescription = Role.RoleDescription,
			IsActive = Role.IsActive
		};
	}

	void Cancel() => EditRoleDialog!.Cancel();

	private void ToggleStatus() => EditRole.IsActive = !EditRole.IsActive;

	async Task Submit()
	{
		await EditRoleForm!.ValidateAsync();
		if (EditRoleForm.IsValid)
		{
			EditRoleDialog!.Close(DialogResult.Ok(EditRole));
		}
	}
}
