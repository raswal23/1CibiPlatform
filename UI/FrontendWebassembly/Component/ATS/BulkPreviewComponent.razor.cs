using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.ATS;

public partial class BulkPreviewComponent
{
	[CascadingParameter]
	private IMudDialogInstance BulkPreviewDialog { get; set; } = default!;

	[Parameter]
	public List<string> Headers { get; set; } = [];

	[Parameter]
	public List<List<string>> Rows { get; set; } = [];

	private void Confirm()
	{
		BulkPreviewDialog.Close(DialogResult.Ok(true));
	}

	private void Cancel()
	{
		BulkPreviewDialog.Cancel();
	}
}
