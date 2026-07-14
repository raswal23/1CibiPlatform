namespace FrontendWebassembly.Component.ATS;

public partial class ATSTabComponent
{
	[Parameter]
	public EventCallback<int> ActiveTabChanged { get; set; }

	private int _activeIndex = 1;
	private async Task NavigateToTab(int index)
	{
		_activeIndex = index;

		if (ActiveTabChanged.HasDelegate)
			await ActiveTabChanged.InvokeAsync(index);
	}

	private string GetTabClass(int index)
	{
		return _activeIndex == index
			? "ats-tab-active"
			: "ats-tab-inactive";
	}

	private string GetOrderClass()
	{
		return (_activeIndex == 1 || _activeIndex == 2)
			? "ats-tab-active"
			: "ats-tab-inactive";
	}

	private string GetReportClass()
	{
		return (_activeIndex == 3 || _activeIndex == 4)
			? "ats-tab-active"
			: "ats-tab-inactive";
	}
}
