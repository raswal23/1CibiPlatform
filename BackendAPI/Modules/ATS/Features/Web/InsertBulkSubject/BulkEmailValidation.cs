namespace ATS.Features.Web.InsertBulkSubject;

public static class BulkEmailValidation
{
	public static async Task ValidateEmailAddressesAsync(IFormFile? file, ValidationContext<InsertBulkSubjectCommand> context, CancellationToken cancellationToken)
	{
		if (file is null || !string.Equals(System.IO.Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase) || file.Length > 25 * 1024 * 1024)
			return;

		var invalidRows = await ValidateEmailAddressesAsync(file, cancellationToken);
		if (invalidRows.Count > 0)
			context.AddFailure($"Email address is not a valid email in row(s): {string.Join(", ", invalidRows)}.");
	}

	public static async Task<IReadOnlyList<int>> ValidateEmailAddressesAsync(
		IFormFile file,
		CancellationToken ct = default)
	{
		await using var stream = file.OpenReadStream();
		using var reader = new StreamReader(stream);
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

		if (!await csv.ReadAsync() || !csv.ReadHeader())
			return [];

		var emailAddressIndex = Array.FindIndex(
			csv.HeaderRecord ?? [],
			header => string.Equals(
				header?.Trim(),
				nameof(BulkUploadCsvRecord.EmailAddress),
				StringComparison.OrdinalIgnoreCase));

		if (emailAddressIndex < 0)
			return [];

		var invalidRows = new List<int>();
		var rowNumber = 1;

		while (await csv.ReadAsync())
		{
			ct.ThrowIfCancellationRequested();
			rowNumber++;

			// A blank cell is deliberately not an upload-time failure: the background
			// parser rejects only that row, instead of this check rejecting the file.
			var value = csv.GetField(emailAddressIndex)?.Trim();
			if (!string.IsNullOrEmpty(value) && !BulkSubjectRowValidator.IsValidEmail(value))
				invalidRows.Add(rowNumber);
		}

		return invalidRows;
	}
}
