namespace FrontendWebassembly.Component.UserManagement;

public partial class AddSubMenuComponent
{
	private MudForm? AddSubMenuForm;

	[CascadingParameter] IMudDialogInstance? MudDialog { get; set; }

	[Parameter]
	public AddSubMenuDTO SubMenu { get; set; } = new AddSubMenuDTO { IsActive = true };

	void Cancel() => MudDialog!.Cancel();

	async Task Submit()
	{
		await AddSubMenuForm!.ValidateAsync();
		if (AddSubMenuForm!.IsValid)
		{
			MudDialog!.Close(DialogResult.Ok(SubMenu));
		}
	}
}
