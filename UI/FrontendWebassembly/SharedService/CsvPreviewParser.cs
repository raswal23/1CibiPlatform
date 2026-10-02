namespace FrontendWebassembly.SharedService;

/// <summary>
/// Parses CSV text for the bulk-upload preview dialog.
/// </summary>
/// <remarks>
/// RFC 4180: fields may be quoted, quoted fields may contain commas, newlines and
/// escaped quotes (""). The preview used to split on ',' and '\n' by hand, so a row like
/// <c>"Dela Cruz, Jr.",Juan,...</c> previewed misaligned - and the operator was
/// approving a preview that did not match what CsvHelper would import on the server.
/// </remarks>
public static class CsvPreviewParser
{
	/// <summary>
	/// The columns the bulk upload imports, in template order. Spreadsheets often pick
	/// up spare columns after these (notes, helper formulas, a stray cell), and the
	/// import ignores them - so the preview must too, or their blank cells block a
	/// perfectly importable file.
	/// </summary>
	public static readonly IReadOnlyList<string> CanonicalHeaders =
	[
		"LastName",
		"FirstName",
		"MiddleInitial",
		"EmailAddress",
		"MobileNumber"
	];

	/// <summary>
	/// The extra columns a data-screening file must carry, appended to the canonical
	/// set in template order. A data candidate is never sent an application form, so
	/// these values cannot be collected later - a file without them would upload and
	/// then have every single row rejected by the background processor.
	/// </summary>
	public static readonly IReadOnlyList<string> IdentityHeaders =
	[
		"DateOfBirth",
		"SSSNumber",
		"TINNumber"
	];

	/// <summary>
	/// The columns the file must lead with for the chosen screening type. Manual files
	/// stop at MobileNumber; anything they carry past it is debris and is dropped.
	/// </summary>
	public static IReadOnlyList<string> HeadersFor(bool requiresIdentity) =>
		requiresIdentity
			? [.. CanonicalHeaders, .. IdentityHeaders]
			: CanonicalHeaders;

	public sealed class CsvPreviewResult
	{
		public List<string> Headers { get; set; } = [];

		public List<List<string>> Rows { get; set; } = [];

		/// <summary>Total data rows in the file, even when more than MaxPreviewRows.</summary>
		public int TotalRowCount { get; set; }

		public bool IsTruncated => TotalRowCount > Rows.Count;

		/// <summary>
		/// Expected columns the file does not contain at all, for the screening type it
		/// was parsed against. Extra columns are ignorable; missing ones are not - the
		/// import cannot map them, so the caller should block before upload rather than
		/// let the file fail server-side.
		/// </summary>
		public List<string> MissingHeaders { get; set; } = [];

		/// <summary>
		/// True when the file leads with the expected columns in exactly the template's
		/// sequence. The template order is the standard: a file with the right columns
		/// in the wrong order is rejected, not reordered.
		/// </summary>
		public bool HasCanonicalHeaderSequence { get; set; }
	}

	/// <param name="requiresIdentity">
	/// True for a data-screening upload, whose rows must also carry DateOfBirth,
	/// SSSNumber and TINNumber. Defaults to false, the manual template.
	/// </param>
	public static CsvPreviewResult Parse(string csvContent, bool requiresIdentity = false)
	{
		var result = new CsvPreviewResult();
		var expectedHeaders = HeadersFor(requiresIdentity);

		if (string.IsNullOrWhiteSpace(csvContent))
			return result;

		var records = ParseRecords(csvContent);

		if (records.Count == 0)
			return result;

		var fileHeaders = records[0].Select(field => field.Trim()).ToList();

		// The template's order is the standard: the file must LEAD with the expected
		// columns in exactly that sequence (trim + ignore-case, as the import applies).
		// Columns after them are spreadsheet debris (notes, helper formulas) - dropped
		// here so they can neither show up in the dialog nor block the upload.
		result.HasCanonicalHeaderSequence =
			fileHeaders.Count >= expectedHeaders.Count
			&& expectedHeaders
				.Select((expected, index) =>
					string.Equals(fileHeaders[index], expected, StringComparison.OrdinalIgnoreCase))
				.All(matches => matches);

		var keptCount = Math.Min(expectedHeaders.Count, fileHeaders.Count);
		var keptIndexes = Enumerable.Range(0, keptCount).ToList();

		result.Headers = keptIndexes.Select(index => fileHeaders[index]).ToList();

		result.MissingHeaders = expectedHeaders
			.Where(expected => !fileHeaders.Contains(expected, StringComparer.OrdinalIgnoreCase))
			.ToList();

		// Ignore rows that are blank in the kept columns - a trailing newline is not a
		// record, and neither is a row whose only content sits in the dropped debris
		// columns (a note or helper formula past MobileNumber). Judging blankness on
		// the whole record used to surface those as all-"(Blank)" rows that blocked
		// an otherwise importable file.
		var dataRows = records
			.Skip(1)
			.Where(fields => keptIndexes.Any(index =>
				index < fields.Count && !string.IsNullOrWhiteSpace(fields[index])))
			.ToList();

		result.TotalRowCount = dataRows.Count;
		result.Rows = dataRows

			.Select(fields => keptIndexes
				.Select(index => index < fields.Count ? fields[index].Trim() : string.Empty)
				.ToList())
			.ToList();

		return result;
	}

	/// <summary>
	/// Parses CSV text for preview purposes, showing ALL columns in the file regardless of screening type.
	/// </summary>
	public static CsvPreviewResult ParseForPreview(string csvContent)
	{
		var result = new CsvPreviewResult();

		if (string.IsNullOrWhiteSpace(csvContent))
			return result;

		var records = ParseRecords(csvContent);

		if (records.Count == 0)
			return result;

		var fileHeaders = records[0].Select(field => field.Trim()).ToList();
		result.Headers = fileHeaders;

		// Show ALL columns in the preview, regardless of screening type
		var allIndexes = Enumerable.Range(0, fileHeaders.Count).ToList();

		// Ignore rows that are blank in any of the columns - a trailing newline is not a
		// record, and neither is a row whose only content sits in the dropped debris
		// columns (a note or helper formula past MobileNumber). Judging blankness on
		// the whole record used to surface those as all-"(Blank)" rows that blocked
		// an otherwise importable file.
		var dataRows = records
			.Skip(1)
			.Where(fields => allIndexes.Any(index =>
				index < fields.Count && !string.IsNullOrWhiteSpace(fields[index])))
			.ToList();

		result.TotalRowCount = dataRows.Count;
		result.Rows = dataRows

			.Select(fields => allIndexes
				.Select(index => index < fields.Count ? fields[index].Trim() : string.Empty)
				.ToList())
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
