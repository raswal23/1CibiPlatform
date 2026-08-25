namespace FrontendWebassembly.Component.Generic;

public partial class PasswordStrengthComponent
{
	[Parameter] public string? Password { get; set; }

	private int GetScore()
	{
		if (string.IsNullOrEmpty(Password))
			return 0;

		var score = 0;

		if (Password.Length is >= 6 and <= 100)
			score++;

		if (System.Text.RegularExpressions.Regex.IsMatch(Password, "[A-Z]")
			&& System.Text.RegularExpressions.Regex.IsMatch(Password, "[a-z]"))
			score++;

		if (System.Text.RegularExpressions.Regex.IsMatch(Password, "[0-9]"))
			score++;

		if (System.Text.RegularExpressions.Regex.IsMatch(Password, @"[\W_]"))
			score++;

		return score;
	}

	private string GetContainerClass()
	{
		if (string.IsNullOrEmpty(Password))
			return "password-strength";

		return GetScore() switch
		{
			4 => "password-strength strong",
			3 => "password-strength good",
			2 => "password-strength fair",
			_ => "password-strength weak"
		};
	}

	private string GetBarClass(int barNumber)
	{
		if (string.IsNullOrEmpty(Password))
			return string.Empty;

		var visibleScore = Math.Max(1, GetScore());
		return barNumber <= visibleScore ? "active" : string.Empty;
	}

	private string GetLabel()
	{
		if (string.IsNullOrEmpty(Password))
			return "Use 6+ characters with uppercase, lowercase, a number and a symbol";

		return GetScore() switch
		{
			4 => "Strong password",
			3 => "Good password",
			2 => "Fair password",
			_ => "Weak password"
		};
	}
}
