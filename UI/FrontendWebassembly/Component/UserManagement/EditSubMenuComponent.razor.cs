namespace FrontendWebassembly.Component.UserManagement;

public partial  class EditSubMenuComponent
{
	private MudForm? EditSubMenuForm;

	[CascadingParameter] IMudDialogInstance? EditSubMenuDialog { get; set; }

	[Parameter] public SubMenusDTO SubMenu { get; set; } = new SubMenusDTO();

	void Cancel() => EditSubMenuDialog!.Cancel();

	async Task Submit()
	{
		await EditSubMenuForm!.ValidateAsync();
		if (EditSubMenuForm!.IsValid)
		{
			EditSubMenuDialog!.Close(DialogResult.Ok(SubMenu));
		}
	}
}
