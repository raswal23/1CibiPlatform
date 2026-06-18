namespace FrontendWebassembly.Component.Generic;

public partial class ErrorDialogComponent
{
	[Parameter]
	public string? TraceId { get; set; }

	[Parameter]
	public string? Message { get; set; }
}
