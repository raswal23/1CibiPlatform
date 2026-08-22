using FrontendWebassembly.ShareData.Auth;

namespace FrontendWebassembly.Component.UserManagement;

public partial class AddSubMenuComponent
{
	private MudForm? AddSubMenuForm;

	[CascadingParameter] IMudDialogInstance? MudDialog { get; set; }

	[Parameter]
	public AddSubMenuDTO SubMenu { get; set; } = new AddSubMenuDTO { IsActive = true };

	private Task<IEnumerable<string>> SearchSubMenus(string value, CancellationToken cancellationToken)
	{
		var result = SubMenuList.List.Values
			.Select(x => x.Name);

		if (!string.IsNullOrWhiteSpace(value))
		{
			result = result.Where(x =>
				x.Contains(value, StringComparison.OrdinalIgnoreCase));
		}

		return Task.FromResult(result);
	}

	void Cancel() => MudDialog!.Cancel();

	private void ToggleStatus() => SubMenu.IsActive = !SubMenu.IsActive;

	async Task Submit()
	{
		await AddSubMenuForm!.ValidateAsync();
		if (AddSubMenuForm!.IsValid)
		{
			MudDialog!.Close(DialogResult.Ok(SubMenu));
		}
	}
}
