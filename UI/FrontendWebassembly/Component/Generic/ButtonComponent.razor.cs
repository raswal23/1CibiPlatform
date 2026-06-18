using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.Generic;

public partial class ButtonComponent
{
	[Parameter]
	public EventCallback OnClick { get; set; }

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public string? Style { get; set; }

	[Parameter]
	public string? Class { get; set; }

	[Parameter]
	public string? StartIcon { get; set; }

	[Parameter]
	public string? EndIcon { get; set; }

	[Parameter]
	public bool Disabled { get; set; } = false;

	[Parameter]
	public string? Href { get; set; }

	[Parameter]
	public MudBlazor.ButtonType ButtonType { get; set; } = MudBlazor.ButtonType.Button;

	[Parameter]
	public MudBlazor.Variant Variant { get; set; } = MudBlazor.Variant.Filled;

	[Parameter]
	public MudBlazor.Color Color { get; set; }

	private string FinalStyle =>
		$"{(string.IsNullOrWhiteSpace(Style) ? string.Empty : $" {Style}")}";

	private string BaseGradientStyle =>
		"background: linear-gradient(90deg,#102247 0%,#2a77ae 50%,#68c0d6 100%); color: white; text-transform: none;";
}