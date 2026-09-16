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
	
	[Parameter]
	public bool? IsDataScreening { get; set; } = null;
	
	private async Task Confirm()
	{
		if (InvalidRows.Any())
		{

			Snackbar.Add("Error. Bulk Submit Failed. Blank or invalid details found", Severity.Error);

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
				"Depending on the selected screening type, an email invitation may be sent to the candidate to complete the required information."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Upload"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"By clicking ' Proceed ,' you attest and confirm that you have obtained the necessary and valid consent from the concerned individual(s) authorizing CIBI Information, Inc. to collect, process, verify, and validate their personal information for the purpose of conducting the requested background verification. You further confirm that the individual(s) have been appropriately informed of the nature and purpose of the background verification and that such consent was obtained prior to submitting this request."
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

	private bool IsEmailAddressColumn(int columnIndex) =>
		columnIndex < Headers.Count
		&& Headers[columnIndex].Replace(" ", string.Empty)
			.Equals("EmailAddress", StringComparison.OrdinalIgnoreCase);

	// ValidateEmail returns null for a blank value, so a blank email is reported once,
	// by the required-cell rule, not twice. The server re-checks malformed emails at
	// upload time (BulkEmailValidation); this catches them before the file leaves the
	// browser, with the same row-numbered message.
	private bool IsInvalidEmail(int columnIndex, string cell) =>
		IsEmailAddressColumn(columnIndex)
		&& EmailValidationService.ValidateEmail(cell.Trim()) is not null;

	// Identity headers that are only required for data screening
	private static bool IsIdentityHeader(string header) =>
		header.Replace(" ", string.Empty).Equals("DateOfBirth", StringComparison.OrdinalIgnoreCase) ||
		header.Replace(" ", string.Empty).Equals("SSSNumber", StringComparison.OrdinalIgnoreCase) ||
		header.Replace(" ", string.Empty).Equals("TINNumber", StringComparison.OrdinalIgnoreCase);

	private bool IsRequiredCellBlank(int columnIndex, string cell) =>
		string.IsNullOrWhiteSpace(cell)
		&& (columnIndex >= Headers.Count || 
		    (!IsOptionalHeader(Headers[columnIndex]) && 
		     !(IsDataScreening == false && IsIdentityHeader(Headers[columnIndex]))));

	private bool IsDateOfBirthColumn(int columnIndex) =>
		columnIndex < Headers.Count
		&& Headers[columnIndex].Replace(" ", string.Empty)
			.Equals("DateOfBirth", StringComparison.OrdinalIgnoreCase);

	private bool IsSSSNumberColumn(int columnIndex) =>
		columnIndex < Headers.Count
		&& Headers[columnIndex].Replace(" ", string.Empty)
			.Equals("SSSNumber", StringComparison.OrdinalIgnoreCase);

	private bool IsTINNumberColumn(int columnIndex) =>
		columnIndex < Headers.Count
		&& Headers[columnIndex].Replace(" ", string.Empty)
			.Equals("TINNumber", StringComparison.OrdinalIgnoreCase);

	private bool IsInvalidDateOfBirth(int columnIndex, string cell) =>
		IsDateOfBirthColumn(columnIndex)
		&& !string.IsNullOrWhiteSpace(cell)
		&& !TryParseDateOfBirth(cell.Trim());

	private bool IsInvalidSSSNumber(int columnIndex, string cell) =>
		IsSSSNumberColumn(columnIndex)
		&& !string.IsNullOrWhiteSpace(cell)
		&& !IsValidSSSNumber(cell.Trim());

	private bool IsInvalidTINNumber(int columnIndex, string cell) =>
		IsTINNumberColumn(columnIndex)
		&& !string.IsNullOrWhiteSpace(cell)
		&& !IsValidTINNumber(cell.Trim());

	// Parse date in MM/dd/yyyy format
	private bool TryParseDateOfBirth(string dateStr) =>
		DateOnly.TryParseExact(dateStr, "MM/dd/yyyy", out var date) && 
		date < DateOnly.FromDateTime(DateTime.Now);

	// SSS number must contain only digits and be exactly 10 digits
	private bool IsValidSSSNumber(string sssNum) =>
		sssNum.All(char.IsDigit) && sssNum.Length == 10;

	// TIN number must contain only digits and be 9 to 12 digits
	private bool IsValidTINNumber(string tinNum) =>
		tinNum.All(char.IsDigit) && tinNum.Length >= 9 && tinNum.Length <= 12;

	private bool IsInvalidCell(int columnIndex, string cell) =>
		IsRequiredCellBlank(columnIndex, cell)
		|| IsInvalidMobileNumber(columnIndex, cell)
		|| IsInvalidEmail(columnIndex, cell)
		|| IsInvalidDateOfBirth(columnIndex, cell)
		|| IsInvalidSSSNumber(columnIndex, cell)
		|| IsInvalidTINNumber(columnIndex, cell);

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

	private List<int> InvalidEmailRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsInvalidEmail(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();

	private List<int> InvalidDateOfBirthRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsInvalidDateOfBirth(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();

	private List<int> InvalidSSSNumberRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsInvalidSSSNumber(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();

	private List<int> InvalidTINNumberRows =>
	Rows
		.Select((row, index) => new { row, index })
		.Where(x => x.row.Where((cell, cellIndex) => IsInvalidTINNumber(cellIndex, cell)).Any())
		.Select(x => x.index + 2)
		.ToList();
}
