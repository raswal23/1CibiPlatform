namespace FrontendWebassembly.Component.PhilSys;

public partial class PhilSysFormTabComponent
{
	[Parameter]
	public EventCallback<int> ActiveTabChanged { get; set; }

	private int _activeIndex = 0;

	private void OnTabChanged(int index)
	{
		_activeIndex = index;
	}

	private async Task TabChangedAsync()
	{
		await ActiveTabChanged.InvokeAsync(_activeIndex);
	}

	private string GetTabClass(int index)
	{
		return _activeIndex == index
			? "philsys-tab-pannel philsys-tab-pannel-active"
			: "philsys-tab-pannel philsys-tab-pannel-inactive";
	}
}
