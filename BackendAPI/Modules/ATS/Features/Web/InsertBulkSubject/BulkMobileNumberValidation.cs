namespace ATS.Features.Web.InsertBulkSubject;

public static class BulkMobileNumberValidation
{
	public static async Task ValidateMobileNumbersAsync(IFormFile? file, ValidationContext<InsertBulkSubjectCommand> context, CancellationToken cancellationToken)
	{
		if (file is null || !string.Equals(System.IO.Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase) || file.Length > 25 * 1024 * 1024)
			return;

		var invalidRows = await ValidateMobileNumbersAsync(file, cancellationToken);
		if (invalidRows.Count > 0)
			context.AddFailure($"Mobile number must be no more than 11 digits in row(s): {string.Join(", ", invalidRows)}.");
	}

	public static async Task<IReadOnlyList<int>> ValidateMobileNumbersAsync(
		IFormFile file,
		CancellationToken ct = default)
	{
		await using var stream = file.OpenReadStream();
		using var reader = new StreamReader(stream);
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

		if (!await csv.ReadAsync() || !csv.ReadHeader())
			return [];

		var mobileNumberIndex = Array.FindIndex(
			csv.HeaderRecord ?? [],
			header => string.Equals(
				header?.Trim(),
				nameof(BulkUploadCsvRecord.MobileNumber),
				StringComparison.OrdinalIgnoreCase));

		if (mobileNumberIndex < 0)
			return [];

		var invalidRows = new List<int>();
		var rowNumber = 1;

		while (await csv.ReadAsync())
		{
			ct.ThrowIfCancellationRequested();
			rowNumber++;

			if (csv.GetField(mobileNumberIndex)?.Trim().Length > 11)
				invalidRows.Add(rowNumber);
		}

		return invalidRows;
	}
}
