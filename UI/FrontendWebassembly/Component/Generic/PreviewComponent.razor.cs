using FrontendWebassembly.Component.ATS;

namespace FrontendWebassembly.Component.Generic;

public partial class PreviewComponent
{
	[CascadingParameter]
	private IMudDialogInstance PreviewDialog { get; set; } = default!;

	[Parameter]
	public List<string> Headers { get; set; } = [];

	[Parameter]
	public List<List<string>> Rows { get; set; } = [];

	private async Task Confirm()
	{
		if (InvalidRows.Any())
		{
			var confirmParam = new DialogParameters
			{
				{ nameof(ConfirmationDialogComponent.Message),
				  "Do you want to upload the template information with blank details?" }

			};

			var dialog = await DialogService.ShowAsync<ConfirmationDialogComponent>(
				"Confirmation",
				confirmParam);

			var result = await dialog.Result;

			if (result.Canceled)
				return;
		}




		PreviewDialog.Close(DialogResult.Ok(true));
	}

	private void Cancel()
	{
		PreviewDialog.Cancel();
	}

	private List<int> InvalidRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Any(string.IsNullOrWhiteSpace))
		.Select(x => x.index + 2)
		.ToList();
}
