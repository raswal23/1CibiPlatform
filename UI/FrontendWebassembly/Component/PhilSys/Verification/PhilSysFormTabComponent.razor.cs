namespace FrontendWebassembly.Component.PhilSys;

public partial class PhilSysFormTabComponent
{
	[Parameter]
	public EventCallback<int> ActiveTabChanged { get; set; }

	private int _activeIndex = 0;

	private async Task SelectTabAsync(int index)
	{
		_activeIndex = index;
		await ActiveTabChanged.InvokeAsync(_activeIndex);
	}

	private string GetTabClass(int index)
	{
		return $"philsys-lookup-tab philsys-tab-button{(_activeIndex == index ? " active" : "")}";
	}
}
