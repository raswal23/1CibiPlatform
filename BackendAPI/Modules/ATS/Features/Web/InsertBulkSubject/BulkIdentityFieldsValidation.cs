using System.Text.Json;

namespace ATS.Features.Web.InsertBulkSubject;

public static class BulkIdentityFieldsValidation
{
	public static async Task ValidateIdentityFieldsAsync(IFormFile? inputFile, ValidationContext<InsertBulkSubjectCommand> context, CancellationToken cancellationToken)
	{
		if (inputFile is null || !string.Equals(System.IO.Path.GetExtension(inputFile.FileName), ".csv", StringComparison.OrdinalIgnoreCase) || inputFile.Length > 25 * 1024 * 1024)
			return;

		// Get the screening type from the command to determine if validation should be applied
		var screeningType = context.InstanceToValidate is InsertBulkSubjectCommand cmd 
			? cmd.bulkUploadFileDetailsDTO.AutoChasing 
			: null;

		var validationResults = await ValidateIdentityFieldsWithScreeningTypeAsync(inputFile, screeningType, cancellationToken);
		
		// Report Date of Birth validation errors (only if screening type is data - false)
		if (screeningType == false && validationResults.DateOfBirthErrors.Count > 0)
		{
			context.AddFailure($"Date of birth validation failed in row(s): {string.Join(", ", validationResults.DateOfBirthErrors.Select(err => $"Row {err.RowNumber} ({err.Error})"))}");
		}
		
		// Report SSS Number validation errors (only if screening type is data - false)
		if (screeningType == false && validationResults.SssNumberErrors.Count > 0)
		{
			context.AddFailure($"SSS number validation failed in row(s): {string.Join(", ", validationResults.SssNumberErrors.Select(err => $"Row {err.RowNumber} ({err.Error})"))}");
		}
		
		// Report TIN Number validation errors (only if screening type is data - false)
		if (screeningType == false && validationResults.TinNumberErrors.Count > 0)
		{
			context.AddFailure($"TIN number validation failed in row(s): {string.Join(", ", validationResults.TinNumberErrors.Select(err => $"Row {err.RowNumber} ({err.Error})"))}");
		}
	}

	public static async Task<IdentityValidationResults> ValidateIdentityFieldsWithScreeningTypeAsync(
		IFormFile file,
		bool? screeningType,
		CancellationToken ct = default)
	{
		var results = new IdentityValidationResults();
		
		await using var stream = file.OpenReadStream();
		var csvContent = await CsvTextDecoder.DecodeAsync(stream, ct);
		using var reader = new StringReader(csvContent);
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

		if (!await csv.ReadAsync() || !csv.ReadHeader())
			return results;

		// Find the indexes of the identity fields
		var dateOfBirthIndex = Array.FindIndex(
			csv.HeaderRecord ?? [],
			header => string.Equals(
				header?.Trim(),
				nameof(BulkUploadCsvRecord.DateOfBirth),
				StringComparison.OrdinalIgnoreCase));

		var sssNumberIndex = Array.FindIndex(
			csv.HeaderRecord ?? [],
			header => string.Equals(
				header?.Trim(),
				nameof(BulkUploadCsvRecord.SSSNumber),
				StringComparison.OrdinalIgnoreCase));

		var tinNumberIndex = Array.FindIndex(
			csv.HeaderRecord ?? [],
			header => string.Equals(
				header?.Trim(),
				nameof(BulkUploadCsvRecord.TINNumber),
				StringComparison.OrdinalIgnoreCase));

		// If any of the identity fields are not present in the CSV, return empty results
		// (validation will be handled by header validation elsewhere)
		if (dateOfBirthIndex < 0 && sssNumberIndex < 0 && tinNumberIndex < 0)
			return results;

		var rowNumber = 1;

		while (await csv.ReadAsync())
		{
			ct.ThrowIfCancellationRequested();
			rowNumber++;

			// Validate Date of Birth if the column exists
			// For data screening (AutoChasing = false), validate if field exists (even if blank, it must be valid format)
			// For manual screening (AutoChasing = true), only validate if field is not blank
			if (dateOfBirthIndex >= 0)
			{
				var dateOfBirthValue = csv.GetField(dateOfBirthIndex)?.Trim();
				
				if (!string.IsNullOrWhiteSpace(dateOfBirthValue))
				{
					// The template's date format, matching the web form's picker display.
					if (!DateOnly.TryParseExact(dateOfBirthValue, "MM/dd/yyyy", out var dateOfBirth))
					{
						results.DateOfBirthErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "Date of birth must be in MM/dd/yyyy format" });
					}
					else if (dateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
					{
						results.DateOfBirthErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "Date of birth must be in the past" });
					}
				}
				else if (screeningType == false) // For data screening, Date of Birth is required
				{
					results.DateOfBirthErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "Date of birth is required for data screening" });
				}
			}

			// Validate SSS Number if the column exists
			// For data screening (AutoChasing = false), validate if field exists (even if blank, it must be valid format)
			// For manual screening (AutoChasing = true), only validate if field is not blank
			if (sssNumberIndex >= 0)
			{
				var sssValue = csv.GetField(sssNumberIndex)?.Trim();
				
				if (!string.IsNullOrWhiteSpace(sssValue))
				{
					if (sssValue.Length != 10 || !sssValue.All(char.IsDigit))
					{
						results.SssNumberErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "SSS number must be 10 digits" });
					}
				}
				else if (screeningType == false) // For data screening, SSS Number is required
				{
					results.SssNumberErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "SSS number is required for data screening" });
				}
			}

			// Validate TIN Number if the column exists
			// For data screening (AutoChasing = false), validate if field exists (even if blank, it must be valid format)
			// For manual screening (AutoChasing = true), only validate if field is not blank
			if (tinNumberIndex >= 0)
			{
				var tinValue = csv.GetField(tinNumberIndex)?.Trim();
				
				if (!string.IsNullOrWhiteSpace(tinValue))
				{
					if (tinValue.Length is < 9 or > 12 || !tinValue.All(char.IsDigit))
					{
						results.TinNumberErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "TIN number must be 9 to 12 digits" });
					}
				}
				else if (screeningType == false) // For data screening, TIN Number is required
				{
					results.TinNumberErrors.Add(new ValidationError { RowNumber = rowNumber, Error = "TIN number is required for data screening" });
				}
			}
		}

		return results;
	}

	// Overload to maintain backward compatibility
	public static async Task<IdentityValidationResults> ValidateIdentityFieldsAsync(
		IFormFile file,
		CancellationToken ct = default)
	{
		// Default to null screening type for backward compatibility
		return await ValidateIdentityFieldsWithScreeningTypeAsync(file, null, ct);
	}

	public class IdentityValidationResults
	{
		public List<ValidationError> DateOfBirthErrors { get; set; } = new();
		public List<ValidationError> SssNumberErrors { get; set; } = new();
		public List<ValidationError> TinNumberErrors { get; set; } = new();
	}

	public class ValidationError
	{
		public int RowNumber { get; set; }
		public string Error { get; set; } = string.Empty;
	}
}