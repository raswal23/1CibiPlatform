namespace FrontendWebassembly.Pages.SSO;

public partial class Frontpage
{
	private bool isLoading = true;

	protected override async Task OnInitializedAsync()
	{
		var isAuthenticated = await SSOService.IsUserAuthenticatedAsync();

		if (!isAuthenticated)
		{
			NavigationManager.NavigateTo("https://console.jumpcloud.com/login");

			return;
		}

		isLoading = false;
	}
}
