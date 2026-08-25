namespace FrontendWebassembly.Component.Generic;

public partial class PasswordStrengthComponent
{
	[Parameter] public string? Password { get; set; }

	// Mirrors RegisterRequestCommandValidator / UpdatePasswordHandler: the meter
	// must never read as valid while the backend would still reject the password.
	private (string Hint, bool Met)[] GetRequirements()
	{
		var password = Password ?? string.Empty;

		return
		[
			("Use 6–100 characters", password.Length is >= 6 and <= 100),
			("Add an uppercase letter", System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]")),
			("Add a lowercase letter", System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]")),
			("Add a number", System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]")),
			("Add a special character", System.Text.RegularExpressions.Regex.IsMatch(password, @"[\W_]"))
		];
	}

	private int GetMetCount() => GetRequirements().Count(r => r.Met);

	private string GetContainerClass()
	{
		if (string.IsNullOrEmpty(Password))
			return "password-strength";

		return GetMetCount() switch
		{
			5 => "password-strength strong",
			4 => "password-strength good",
			3 => "password-strength fair",
			_ => "password-strength weak"
		};
	}

	private string GetBarClass(int barNumber)
	{
		if (string.IsNullOrEmpty(Password))
			return string.Empty;

		var activeBars = Math.Clamp(GetMetCount() - 1, 1, 4);
		return barNumber <= activeBars ? "active" : string.Empty;
	}

	private string GetLabel()
	{
		if (string.IsNullOrEmpty(Password))
			return "Use 6+ characters with uppercase, lowercase, a number and a symbol";

		var missing = GetRequirements().FirstOrDefault(r => !r.Met);

		if (missing.Hint is null)
			return "Strong password";

		var strengthWord = GetMetCount() switch
		{
			4 => "Good",
			3 => "Fair",
			_ => "Weak"
		};

		return $"{strengthWord} — {missing.Hint.ToLowerInvariant()}";
	}
}
