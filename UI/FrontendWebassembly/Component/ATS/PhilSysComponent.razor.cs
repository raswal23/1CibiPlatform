using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.ATS;

public partial class PhilSysComponent
{
	private int _activeIndex = 0;
	[Parameter] public string? HashToken { get; set; }

	private void ChangeActiveTab(int value)
	{
		_activeIndex = value;
	}
}
