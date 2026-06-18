namespace FrontendWebassembly.Component.ATS;

public partial class SuccessSaveComponent
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public string Message { get; set; } = string.Empty;

	private void Close()
	{
		MudDialog.Close();
	}
}
