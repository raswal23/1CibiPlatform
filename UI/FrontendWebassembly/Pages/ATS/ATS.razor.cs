namespace FrontendWebassembly.Pages.ATS;

public partial class ATS
{
	private int _activeIndex = 0;

	private bool _isLoading = false;

	private async Task ChangeActiveTab(int value)
	{
		_isLoading = true;
		StateHasChanged();

		await Task.Delay(50);

		_activeIndex = value;

		_isLoading = false;
	}
}
