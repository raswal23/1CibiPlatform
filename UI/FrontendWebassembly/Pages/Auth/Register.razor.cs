namespace FrontendWebassembly.Pages.Auth;

public partial class Register
{
	private MudForm form;
	private bool success;
	private bool isLoading = false;
	private bool _isLoading = true;

	private string firstName = "";
	private string lastName = "";
	private string middleName = "";
	private string email = "";
	private string password = "";
	private string confirmPassword = "";
	private bool isUserValid = true;
	private string errorMessage = "";

	private bool isPasswordVisible = false;
	private InputType passwordInput = InputType.Password;
	private string passwordIcon = Icons.Material.Filled.VisibilityOff;

	private bool isConfirmPasswordVisible = false;
	private InputType confirmPasswordInput = InputType.Password;
	private string confirmPasswordIcon = Icons.Material.Filled.VisibilityOff;

	protected override async Task OnInitializedAsync()
	{
		var isAuthenticated = await IAuthService.IsAuthenticated();

		if (isAuthenticated)
		{

			Navigation.NavigateTo("/", true);
			return;
		}

		_isLoading = false;
	}

	private void TogglePasswordVisibility()
	{
		isPasswordVisible = !isPasswordVisible;
		passwordInput = isPasswordVisible ? InputType.Text : InputType.Password;
		passwordIcon = isPasswordVisible ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;
	}

	private void ToggleConfirmPasswordVisibility()
	{
		isConfirmPasswordVisible = !isConfirmPasswordVisible;
		confirmPasswordInput = isConfirmPasswordVisible ? InputType.Text : InputType.Password;
		confirmPasswordIcon = isConfirmPasswordVisible ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;
	}

	private string ValidateEmail(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
			return "Email is required";

		if (!email.Contains("@"))
			return "Invalid email format";

		return null;
	}

	private string ValidatePassword(string password)
	{
		if (string.IsNullOrWhiteSpace(password))
			return "Password is required";

		if (password.Length < 8)
			return "Password must be at least 8 characters";

		return null;
	}

	private string ValidateConfirmPassword(string confirmPassword)
	{
		if (string.IsNullOrWhiteSpace(confirmPassword))
			return "Please confirm your password";

		if (confirmPassword != password)
			return "Passwords do not match";

		return null;
	}

	private async Task HandleRegister()
	{
		isLoading = true;
		isUserValid = true;
		errorMessage = "";

		try
		{
			var registerData = new RegisterRequestDTO(
				Email: email,
				PasswordHash: password,
				FirstName: firstName,
				LastName: lastName,
				MiddleName: string.IsNullOrWhiteSpace(middleName) ? null : middleName
			);

			var result = await IAuthService.Register(registerData);

			if (!string.IsNullOrWhiteSpace(result.errorMessage))
			{
				Console.WriteLine($"Register Error: {result.errorMessage}");
				isLoading = false;
				isUserValid = false;
				errorMessage = result.errorMessage;
				StateHasChanged();
				return;
			}
			await LocalStorageService.SetItemAsync("tempUserId", result.id);
			await LocalStorageService.SetItemAsync("tempUserEmail", result.email);
			Navigation.NavigateTo("/verify-otp", true);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Register Exception: {ex.Message}");
			isLoading = false;
			isUserValid = false;
			errorMessage = "An unexpected error occurred. Please try again.";
			StateHasChanged();
			return;
		}
	}
}
