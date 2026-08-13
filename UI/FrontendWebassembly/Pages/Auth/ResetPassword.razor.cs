namespace FrontendWebassembly.Pages.Auth;

public partial class ResetPassword
{
	[Parameter]
	[SupplyParameterFromQuery(Name = "token")]
	public string? token { get; set; }

	private MudForm form;
	private bool formValid;
	private bool isLoading = false;
	private bool isButtonLoading = false;
	private bool isDisable = false;
	private bool isUserValid = true;
	private bool isValidationError = false;
	private bool isSuccess = false;
	private string errorMessage = "";
	private string successMessage = "";
	private string newPassword = "";
	private string confirmPassword = "";
	private bool isPasswordVisible = false;
	private InputType passwordInput = InputType.Password;
	private string passwordIcon = Icons.Material.Filled.VisibilityOff;
	private Guid userId = Guid.Empty;
	private bool tokenValid = false;
	private int redirectCountdown = 5;
	private System.Threading.Timer? countdownTimer;

	protected override async Task OnInitializedAsync()
	{

		if (string.IsNullOrWhiteSpace(token))
		{
			isUserValid = false;
			errorMessage = "Invalid or missing token.";
			return;
		}

		isLoading = true;
		var tokenResponse = await IAuthService.IsForgotPasswordTokenValid(new ForgotPasswordTokenRequestDTO(token));

		if (!tokenResponse.IsValid)
		{
			isUserValid = false;
			errorMessage = tokenResponse.errorMessage ?? "Invalid or expired token.";
			isLoading = false;
			return;
		}

		tokenValid = true;
		isLoading = false;
	}

	private void TogglePasswordVisibility()
	{
		isPasswordVisible = !isPasswordVisible;
		passwordInput = isPasswordVisible ? InputType.Text : InputType.Password;
		passwordIcon = isPasswordVisible ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;
	}

	private string ValidatePassword(string password)
	{
		if (string.IsNullOrWhiteSpace(password))
			return "Password is required";
		if (password.Length < 6)
			return "Password must be at least 6 characters long";
		if (password.Length > 100)
			return "Password must not exceed 100 characters";
		if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]"))
			return "Password must contain at least one uppercase letter";
		if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]"))
			return "Password must contain at least one lowercase letter";
		if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]"))
			return "Password must contain at least one digit";
		if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[\W_]"))
			return "Password must contain at least one special character";
		return null;
	}

	private int GetPasswordStrengthScore()
	{
		if (string.IsNullOrEmpty(newPassword))
			return 0;

		var score = 0;

		if (newPassword.Length is >= 6 and <= 100)
			score++;

		if (System.Text.RegularExpressions.Regex.IsMatch(newPassword, "[A-Z]")
			&& System.Text.RegularExpressions.Regex.IsMatch(newPassword, "[a-z]"))
			score++;

		if (System.Text.RegularExpressions.Regex.IsMatch(newPassword, "[0-9]"))
			score++;

		if (System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"[\W_]"))
			score++;

		return score;
	}

	private string GetPasswordStrengthContainerClass()
	{
		if (string.IsNullOrEmpty(newPassword))
			return "reset-password-strength";

		return GetPasswordStrengthScore() switch
		{
			4 => "reset-password-strength strong",
			3 => "reset-password-strength good",
			2 => "reset-password-strength fair",
			_ => "reset-password-strength weak"
		};
	}

	private string GetPasswordStrengthBarClass(int barNumber)
	{
		if (string.IsNullOrEmpty(newPassword))
			return string.Empty;

		var visibleScore = Math.Max(1, GetPasswordStrengthScore());
		return barNumber <= visibleScore ? "active" : string.Empty;
	}

	private string GetPasswordStrengthLabel()
	{
		if (string.IsNullOrEmpty(newPassword))
			return "Use 6+ characters with uppercase, lowercase, a number and a symbol";

		return GetPasswordStrengthScore() switch
		{
			4 => "Strong password",
			3 => "Good password",
			2 => "Fair password",
			_ => "Weak password"
		};
	}

	private string ValidateConfirmPassword(string confirm)
	{
		if (string.IsNullOrWhiteSpace(confirm))
			return "Confirm password is required";
		if (confirm != newPassword)
			return "Passwords do not match";
		return null;
	}

	private async Task HandleResetPassword()
	{
		isButtonLoading = true;
		isUserValid = true;
		isSuccess = false;
		errorMessage = "";
		successMessage = "";
		try
		{
			var updateResponse = await IAuthService.UpdatePassword(new UpdatePasswordRequestDTO(token!, newPassword));

			if (!updateResponse.IsSuccessful)
			{
				errorMessage = updateResponse.errorMessage ?? "Failed to reset password.";
				isValidationError = true;
				isButtonLoading = false;
				StateHasChanged();
				return;
			}
			isValidationError = false;
			isDisable = true;
			isSuccess = true;
			successMessage = "Your password has been reset successfully. You can now sign in.";
			StartCountdown();
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
			errorMessage = "An unexpected error occurred. Please try again.";
			isButtonLoading = false;
			isUserValid = false;
		}
		isLoading = false;
		StateHasChanged();
	}

	private void StartCountdown()
	{
		countdownTimer = new System.Threading.Timer(async _ =>
		{
			redirectCountdown--;
			if (redirectCountdown <= 0)
			{
				countdownTimer?.Dispose();
				await InvokeAsync(() => Navigation.NavigateTo("/login", true));
			}
			else
			{
				await InvokeAsync(StateHasChanged);
			}
		}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
	}
}
