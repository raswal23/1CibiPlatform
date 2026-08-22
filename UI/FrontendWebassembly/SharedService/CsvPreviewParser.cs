namespace FrontendWebassembly.SharedService;

/// <summary>
/// Parses CSV text for the bulk-upload preview dialog.
/// </summary>
/// <remarks>
/// RFC 4180: fields may be quoted, quoted fields may contain commas, newlines and
/// escaped quotes (""). The preview used to split on ',' and '\n' by hand, so a row like
/// <c>"Dela Cruz, Jr.",Juan,...</c> previewed misaligned - and the operator was
/// approving a preview that did not match what CsvHelper would import on the server.
///
/// This is a preview, so it is deliberately bounded: <see cref="MaxPreviewRows"/> caps
/// how much is materialised for a browser to render.
/// </remarks>
public static class CsvPreviewParser
{
	/// <summary>
	/// Rows shown in the preview. A 10k-row upload should not build 10k lists of cells
	/// in the browser before a dialog opens.
	/// </summary>
	public const int MaxPreviewRows = 100;

	public sealed class CsvPreviewResult
	{
		public List<string> Headers { get; set; } = [];

		public List<List<string>> Rows { get; set; } = [];

		/// <summary>Total data rows in the file, even when more than MaxPreviewRows.</summary>
		public int TotalRowCount { get; set; }

		public bool IsTruncated => TotalRowCount > Rows.Count;
	}

	public static CsvPreviewResult Parse(string csvContent)
	{
		var result = new CsvPreviewResult();

		if (string.IsNullOrWhiteSpace(csvContent))
			return result;

		var records = ParseRecords(csvContent);

		if (records.Count == 0)
			return result;

		result.Headers = records[0].Select(field => field.Trim()).ToList();

		// Ignore rows that are entirely blank - a trailing newline is not a record.
		var dataRows = records
			.Skip(1)
			.Where(fields => fields.Any(field => !string.IsNullOrWhiteSpace(field)))
			.ToList();

		result.TotalRowCount = dataRows.Count;
		result.Rows = dataRows
			.Take(MaxPreviewRows)
			.Select(fields => fields.Select(field => field.Trim()).ToList())
			.ToList();

		return result;
	}

	/// <summary>
	/// Single-pass RFC 4180 scan. Quotes toggle "in field" mode; a doubled quote inside
	/// a quoted field is a literal quote.
	/// </summary>
	private static List<List<string>> ParseRecords(string content)
	{
		var records = new List<List<string>>();
		var currentRecord = new List<string>();
		var currentField = new StringBuilder();
		var inQuotes = false;

		for (var index = 0; index < content.Length; index++)
		{
			var character = content[index];

			if (inQuotes)
			{
				if (character == '"')
				{
					// "" inside a quoted field is an escaped quote, not the end of it.
					if (index + 1 < content.Length && content[index + 1] == '"')
					{
						currentField.Append('"');
						index++;
					}
					else
					{
						inQuotes = false;
					}
				}
				else
				{
					// Newlines inside quotes belong to the field.
					currentField.Append(character);
				}

				continue;
			}

			switch (character)
			{
				case '"':
					inQuotes = true;
					break;

				case ',':
					currentRecord.Add(currentField.ToString());
					currentField.Clear();
					break;

				case '\r':
					// Swallow CR; the LF that follows ends the record.
					break;

				case '\n':
					currentRecord.Add(currentField.ToString());
					currentField.Clear();
					records.Add(currentRecord);
					currentRecord = [];
					break;

				default:
					currentField.Append(character);
					break;
			}
		}

		// Whatever is left when the input runs out is the last record, unless the file
		// ended on a clean newline.
		if (currentField.Length > 0 || currentRecord.Count > 0)
		{
			currentRecord.Add(currentField.ToString());
			records.Add(currentRecord);
		}

		return records;
	}
}
