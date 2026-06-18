namespace FrontendWebassembly.Component.PhilSys;

public partial class ReTransactComponent : ComponentBase
{
	[Inject]
	protected NavigationManager NavigationManager { get; set; } = default!;

	[Parameter]
	public string? WebHookUrl { get; set; }

	private void StartNewSession()
	{
		NavigationManager.NavigateTo("/philsys/idv");
	}
}
