namespace ATS.Services.AuditTrail;

/// <summary>
/// Renders audit entries as a styled .xlsx workbook.
///
/// Separate from <see cref="AtsAuditService"/> because presentation is not that service's
/// job - it owns the access rule and the query. This class knows nothing about who may read
/// the trail; it is handed rows that have already been authorised.
///
/// ClosedXML rather than CsvHelper (which the bulk subject export uses) because the point of
/// this file is that a reader can SEE the failures: a CSV cannot colour a row, freeze a
/// header or auto-filter a column.
/// </summary>
public static class AtsAuditWorkbookWriter
{
	private const string SheetName = "Audit Trail";

	// The action name AtsAssistantService.RecordAudit writes. Matched on rather than
	// sniffing the payload's shape, which would misfire on any future command carrying a
	// "Question" field.
	private const string AssistantAction = "AskAtsAssistant";

	private const int ColumnCount = 12;

	// Wide enough for a sentence without letting one long stack trace set the column width
	// for the whole sheet. Excel wraps within this.
	private const double CauseColumnWidth = 60;

	// Wider still: this holds a whole chat exchange.
	private const double DetailsColumnWidth = 80;

	// Excel refuses a cell over 32,767 characters. Kept well below so a long transcript
	// cannot fail the whole export.
	private const int MaxDetailsLength = 30_000;

	/// <summary>
	/// Builds the workbook. The returned stream is positioned at 0 and owned by the caller.
	/// </summary>
	public static Stream Write(IReadOnlyList<AuditTrailListDTO> entries)
	{
		using var workbook = new ClosedXML.Excel.XLWorkbook();

		var sheet = workbook.Worksheets.Add(SheetName);

		WriteHeader(sheet);
		WriteRows(sheet, entries);
		ApplyLayout(sheet, entries.Count);

		var content = new MemoryStream();

		workbook.SaveAs(content);

		// SaveAs leaves the position at the end; the endpoint streams from the start.
		content.Position = 0;

		return content;
	}

	private static void WriteHeader(ClosedXML.Excel.IXLWorksheet sheet)
	{
		// Cause sits immediately after Outcome, so the reason a row is red is the next thing
		// the eye reaches rather than the last column off-screen.
		string[] headers =
		[
			"When (UTC)",
			"Action",
			"Area",
			"Outcome",
			"Cause of failure",
			"User",
			"Email",
			"Site",
			"Duration (ms)",
			"Details",
			"IP address",
			"Trace ID"
		];

		for (var column = 0; column < headers.Length; column++)
		{
			sheet.Cell(1, column + 1).Value = headers[column];
		}

		var headerRow = sheet.Row(1);

		headerRow.Style.Font.Bold = true;
		headerRow.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
		headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1C3A70");
		headerRow.Style.Alignment.Vertical = ClosedXML.Excel.XLAlignmentVerticalValues.Center;
		headerRow.Height = 22;
	}

	private static void WriteRows(
		ClosedXML.Excel.IXLWorksheet sheet,
		IReadOnlyList<AuditTrailListDTO> entries)
	{
		for (var index = 0; index < entries.Count; index++)
		{
			var entry = entries[index];

			// +2: row 1 is the header, and ClosedXML is 1-based.
			var rowNumber = index + 2;

			sheet.Cell(rowNumber, 1).Value = entry.OccurredAt;
			sheet.Cell(rowNumber, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";

			sheet.Cell(rowNumber, 2).Value = entry.Action;
			sheet.Cell(rowNumber, 3).Value = entry.Area;
			sheet.Cell(rowNumber, 4).Value = entry.Outcome;

			// The whole reason this export exists. Written as text so a reason that starts
			// with '=' or '+' cannot be interpreted as a formula when the file is opened.
			sheet.Cell(rowNumber, 5).SetValue(entry.FailureReason ?? string.Empty);
			sheet.Cell(rowNumber, 5).Style.Alignment.WrapText = true;

			sheet.Cell(rowNumber, 6).Value = entry.UserFullName ?? string.Empty;
			sheet.Cell(rowNumber, 7).Value = entry.UserEmail ?? string.Empty;
			sheet.Cell(rowNumber, 8).Value = entry.Site ?? string.Empty;
			sheet.Cell(rowNumber, 9).Value = entry.DurationMs;

			// The payload rendered for a human. For an assistant turn this is the actual
			// question and answer, which is the whole point of exporting those rows.
			// SetValue, like the cause column, so text beginning '=' is never a formula.
			sheet.Cell(rowNumber, 10).SetValue(DescribePayload(entry));
			sheet.Cell(rowNumber, 10).Style.Alignment.WrapText = true;

			sheet.Cell(rowNumber, 11).Value = entry.IpAddress ?? string.Empty;
			sheet.Cell(rowNumber, 12).Value = entry.TraceId ?? string.Empty;

			StyleOutcome(sheet, rowNumber, entry.Outcome);
		}
	}

	/// <summary>
	/// Renders an entry's payload as readable text for the Details column.
	///
	/// An assistant transcript gets a Q/A layout, because "what did they ask and what were
	/// they told" is unreadable as raw JSON in a spreadsheet cell. Everything else falls
	/// back to the stored JSON, which is what the detail dialog shows.
	/// </summary>
	private static string DescribePayload(AuditTrailListDTO entry)
	{
		if (string.IsNullOrWhiteSpace(entry.Payload) || entry.Payload == "{}")
		{
			return string.Empty;
		}

		if (!string.Equals(entry.Action, AssistantAction, StringComparison.OrdinalIgnoreCase))
		{
			return Truncate(entry.Payload, MaxDetailsLength);
		}

		try
		{
			using var document = JsonDocument.Parse(entry.Payload);
			var root = document.RootElement;

			var question = ReadString(root, "Question");
			var answer = ReadString(root, "Answer");

			var transcript = $"Q: {question}\n\nA: {answer}";

			// Surfaced as a line rather than left to be inferred from the answer's wording,
			// which may change.
			if (root.TryGetProperty("WasRefused", out var refused)
				&& refused.ValueKind == JsonValueKind.True)
			{
				transcript += "\n\n[refused as out of scope]";
			}

			return Truncate(transcript, MaxDetailsLength);
		}
		catch (JsonException)
		{
			// A payload that will not parse is still worth exporting as-is.
			return Truncate(entry.Payload, MaxDetailsLength);
		}
	}

	private static string ReadString(JsonElement root, string propertyName) =>
		root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	// Excel refuses a cell over 32,767 characters, so this is a hard limit rather than a
	// stylistic one.
	private static string Truncate(string value, int maxLength) =>
		value.Length > maxLength ? value[..maxLength] : value;

	// A failure is tinted across the whole row, not just its Outcome cell: someone scanning
	// a thousand rows for what went wrong should find them without reading a column.
	private static void StyleOutcome(
		ClosedXML.Excel.IXLWorksheet sheet,
		int rowNumber,
		string outcome)
	{
		var isFailure = string.Equals(outcome, AuditOutcome.Failure, StringComparison.OrdinalIgnoreCase);

		if (!isFailure)
		{
			return;
		}

		var row = sheet.Range(rowNumber, 1, rowNumber, ColumnCount);

		row.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FDECEA");
		row.Style.Font.FontColor = ClosedXML.Excel.XLColor.FromHtml("#8C2F2A");

		sheet.Cell(rowNumber, 4).Style.Font.Bold = true;
	}

	private static void ApplyLayout(ClosedXML.Excel.IXLWorksheet sheet, int rowCount)
	{
		// Freeze the header so it stays visible while scrolling a long trail, and switch on
		// auto-filter so a reader can narrow to Failure without writing a formula. Both are
		// the reason this is a workbook rather than a CSV.
		sheet.SheetView.FreezeRows(1);

		var lastRow = Math.Max(rowCount + 1, 1);

		sheet.Range(1, 1, lastRow, ColumnCount).SetAutoFilter();

		sheet.Columns().AdjustToContents();

		// AdjustToContents sizes a column to its longest value, which for Cause and Details
		// can be thousands of characters. Pin both and let the wrap handle the rest.
		sheet.Column(5).Width = CauseColumnWidth;
		sheet.Column(10).Width = DetailsColumnWidth;

		// Keep every other column readable without becoming a wall of text.
		foreach (var column in sheet.ColumnsUsed())
		{
			var number = column.ColumnNumber();

			if (number != 5 && number != 10 && column.Width > 40)
			{
				column.Width = 40;
			}
		}

		// Top-aligned so a wrapped transcript reads from the top of its cell rather than
		// floating in the middle of a tall row.
		sheet.Rows(2, lastRow).Style.Alignment.Vertical =
			ClosedXML.Excel.XLAlignmentVerticalValues.Top;
	}
}
