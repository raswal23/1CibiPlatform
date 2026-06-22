namespace FrontendWebassembly.SharedService;

public class EmailValidationService
{
	public string? ValidateEmail(string value)
	{
		if (value is null)
			return null;

		if (!value.Contains("@"))
			return "Invalid email format";

		var regex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
		if (!Regex.IsMatch(value, regex))
			return "Invalid email format";

		return null;
	}

}
