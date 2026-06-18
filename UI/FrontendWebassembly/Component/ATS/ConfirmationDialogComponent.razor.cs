namespace FrontendWebassembly.Component.ATS;

public partial class ConfirmationDialogComponent
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public string Message { get; set; } = string.Empty;

	private void Confirm()
	{
		MudDialog.Close(DialogResult.Ok(true));
	}

	private void Cancel()
	{
		MudDialog.Cancel();
	}
}
