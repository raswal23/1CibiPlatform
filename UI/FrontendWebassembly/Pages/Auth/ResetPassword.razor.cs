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
		if (password.Length < 8)
			return "Password must be at least 8 characters";
		// Add more password rules as needed
		return null;
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
