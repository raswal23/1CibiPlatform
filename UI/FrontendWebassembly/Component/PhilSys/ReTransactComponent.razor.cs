namespace FrontendWebassembly.Component.PhilSys;

public partial class ReTransactComponent : ComponentBase
{
	[Parameter]
	public string? WebHookUrl { get; set; }
	[Parameter]
	public string? ATSHashToken { get; set; }
	[Parameter]
	public bool IsCompleted { get; set; }
	[Parameter]
	public bool IsTransacted { get; set; }

	private bool DisableButton()
	{
		if (WebHookUrl == "/" || !string.IsNullOrEmpty(ATSHashToken))
		{
			return true;
		}

		return false;
	}

	private void StartNewSession()
	{
		NavigationManager.NavigateTo("/philsys/idv");
	}
}
