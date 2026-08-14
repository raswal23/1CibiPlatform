namespace FrontendWebassembly.Component.Generic;

public partial class ConfirmationDialogComponent
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public string Title { get; set; } = string.Empty;

	[Parameter]
	public string Message { get; set; } = string.Empty;

	[Parameter]
	public string ConfirmText { get; set; } = string.Empty;

	[Parameter]
	public string? InformationMessage { get; set; } = string.Empty;

	[Parameter]
	public Color ConfirmButtonColor { get; set; } = Color.Primary;

	private void Confirm()
	{
		MudDialog.Close(DialogResult.Ok(true));
	}

}