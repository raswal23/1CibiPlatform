namespace FrontendWebassembly.SharedService;

public class FileValidationService
{
	public (bool IsValid, string? ErrorMessage) ValidateExtension(
		string fileName,
		params string[] allowedExtensions)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return (false, "File name is required.");
		}

		var extension = Path.GetExtension(fileName);

		if (allowedExtensions.Any(x =>
				string.Equals(extension, x, StringComparison.OrdinalIgnoreCase)))
		{
			return (true, null);
		}

		return (
			false,
			$"Only the following file type/s are allowed: {string.Join(", ", allowedExtensions)}"
		);
	}
}
