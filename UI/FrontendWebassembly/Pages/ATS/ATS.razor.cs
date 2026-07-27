namespace FrontendWebassembly.Pages.ATS;

public partial class ATS
{
	private int _activeIndex = 2; //change to 0 for dashboard again

	private bool _isLoading = false;

	private async Task ChangeActiveTab(int value)
	{
		_isLoading = true;
		StateHasChanged();

		_activeIndex = value;

		_isLoading = false;
	}
}
