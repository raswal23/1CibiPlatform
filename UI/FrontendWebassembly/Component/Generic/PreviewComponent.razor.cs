namespace FrontendWebassembly.Component.ATS;

public partial class PreviewComponent
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

	private List<int> InvalidRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Any(string.IsNullOrWhiteSpace))
		.Select(x => x.index + 2)
		.ToList();
}
