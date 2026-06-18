namespace FrontendWebassembly.Component.PhilSys;

public partial class LivenessCheckComponent
{
	[Parameter]
	public EventCallback OnClick { get; set; }
	private bool isNavigationLocked = false;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await SetupNavigationLock();
		}
	}

	private async Task SetupNavigationLock()
	{
		if (!isNavigationLocked)
		{
			isNavigationLocked = true;
			await JS.InvokeVoidAsync(
			"navigationLock.enable",
			"You have an unfinished liveness check session. Leaving now could result in lost progress. Are you sure you want to continue?"
			);
		}
	}

	private async Task RemoveNavigationLock()
	{
		if (isNavigationLocked)
		{
			isNavigationLocked = false;
			await JS.InvokeVoidAsync("navigationLock.disable");
		}
	}

	public async ValueTask DisposeAsync()
	{
		await RemoveNavigationLock();
	}

}
