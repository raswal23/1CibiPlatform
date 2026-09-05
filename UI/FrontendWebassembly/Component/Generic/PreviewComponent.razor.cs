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

	private static string GetCandidateInitials(IReadOnlyList<string> row)
	{
		var lastNameInitial = row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]) ? row[0].Trim()[0] : default;
		var firstNameInitial = row.Count > 1 && !string.IsNullOrWhiteSpace(row[1]) ? row[1].Trim()[0] : default;
		return $"{lastNameInitial}{firstNameInitial}".ToUpperInvariant();
	}

	// A candidate legitimately may have no middle initial, so that column is the one
	// blank the upload must not block on. The backend stores it as null.
	private static bool IsOptionalHeader(string header) =>
		header.Replace(" ", string.Empty)
			.Equals("MiddleInitial", StringComparison.OrdinalIgnoreCase);

	private bool IsMobileNumberColumn(int columnIndex) =>
		columnIndex < Headers.Count
		&& Headers[columnIndex].Replace(" ", string.Empty)
			.Equals("MobileNumber", StringComparison.OrdinalIgnoreCase);

	private bool IsInvalidMobileNumber(int columnIndex, string cell) =>
		IsMobileNumberColumn(columnIndex)
		&& !string.IsNullOrWhiteSpace(cell)
		&& cell.Trim().Length != 11;

	private bool IsRequiredCellBlank(int columnIndex, string cell) =>
		string.IsNullOrWhiteSpace(cell)
		&& (columnIndex >= Headers.Count || !IsOptionalHeader(Headers[columnIndex]));

	private bool IsInvalidCell(int columnIndex, string cell) =>
		IsRequiredCellBlank(columnIndex, cell) || IsInvalidMobileNumber(columnIndex, cell);

	private List<int> InvalidRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsInvalidCell(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();

	private List<int> BlankRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsRequiredCellBlank(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();

	private List<int> InvalidMobileRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsInvalidMobileNumber(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();
}
