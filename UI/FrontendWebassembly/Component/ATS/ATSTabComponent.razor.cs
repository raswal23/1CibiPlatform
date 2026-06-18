using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.ATS;

public partial class ATSTabComponent
{
	[Parameter]
	public EventCallback<int> ActiveTabChanged { get; set; }

	private int _activeIndex = 0;

	private async Task TabChangedAsync()
	{
		await ActiveTabChanged.InvokeAsync(_activeIndex);
	}

	private string GetTabClass(int index)
	{
		return _activeIndex == index
			? "philsys-tab-pannel-active"
			: "philsys-tab-pannel-inactive";
	}
}
