namespace FrontendWebassembly.Component.PhilSys;

public partial class NotFoundComponent
{
	[Parameter]
	public string? WebHookUrl { get; set; }

	private void GoBack()
	{
		NavigationManager.NavigateTo("/philsys/idv");
	}
}
