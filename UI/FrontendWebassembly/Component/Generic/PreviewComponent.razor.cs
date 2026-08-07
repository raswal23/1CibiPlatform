namespace FrontendWebassembly.Component.Generic;

public partial class PreviewComponent
{
	[CascadingParameter]
	private IMudDialogInstance PreviewDialog { get; set; } = default!;

	[Parameter]
	public List<string> Headers { get; set; } = [];

	[Parameter]
	public List<List<string>> Rows { get; set; } = [];
	
	[Parameter]
	public string Message { get; set; } = string.Empty;
	private async Task Confirm()
	{
		if (InvalidRows.Any())
		{

			Snackbar.Add("Error. Bulk Submit Failed. Blank details found", Severity.Error);

			return;
		}

		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Bulk Submit Candidate"
			},
			{
				nameof(YesNoDialogComponent.Message),
				"Please be advised that this action will send an email invitation to your candidates."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Upload"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Clicking 'Upload' will  send email invitations."
			}
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(null, confirmParam, options);

		var result = await dialog.Result;

		if (result!.Canceled)
			return;

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
