namespace FrontendWebassembly.SharedService;

public class MobileNumberValidationService
{
	public string? ValidateMobileNumber(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "Mobile Contact Information is required";

		if (value.Length != 11)
			return "Mobile number must be exactly 11 digits.";

		return null;
	}
}
