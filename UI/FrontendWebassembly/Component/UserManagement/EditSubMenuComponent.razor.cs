using FrontendWebassembly.ShareData.Auth;

namespace FrontendWebassembly.Component.UserManagement;

public partial class EditSubMenuComponent
{
	private MudForm? EditSubMenuForm;

	[CascadingParameter]
	IMudDialogInstance? EditSubMenuDialog { get; set; }

	[Parameter]
	public SubMenusDTO SubMenu { get; set; } = new SubMenusDTO();

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

	private SubMenusDTO EditSubMenu = new();

	protected override void OnParametersSet()
	{
		EditSubMenu = new SubMenusDTO
		{
			subMenuId = SubMenu.subMenuId,
			subMenuName = SubMenu.subMenuName,
			Description = SubMenu.Description,
			IsActive = SubMenu.IsActive
		};
	}
	void Cancel() => EditSubMenuDialog!.Cancel();

	async Task Submit()
	{
		await EditSubMenuForm!.ValidateAsync();
		if (EditSubMenuForm!.IsValid)
		{
			EditSubMenuDialog!.Close(DialogResult.Ok(EditSubMenu));
		}
	}
}
