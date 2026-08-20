namespace FrontendWebassembly.Pages.Auth;

public partial class Login
{
	private MudForm? loginForm;
	private MudForm? registerForm;
	private bool loginFormValid;
	private bool registerFormValid;
	private bool isLoginLoading;
	private bool isRegisterLoading;
	private bool _isLoading = true;
	private bool isRegisterMode;
	private bool isForgotPasswordMode;
	private bool hasSwitchedMode;
	private MudForm? forgotPasswordForm;
	private bool forgotPasswordFormValid;
	private bool isForgotPasswordLoading;
	private bool isForgotPasswordUserValid = true;
	private bool isForgotPasswordSuccess;
	private string forgotPasswordEmail = string.Empty;
	private string forgotPasswordErrorMessage = string.Empty;
	private string forgotPasswordSuccessMessage = string.Empty;

	private string loginEmail = string.Empty;
	private string loginPassword = string.Empty;
	private bool rememberMe;
	private bool isLoginUserValid = true;
	private string loginErrorMessage = string.Empty;

	private bool isLoginPasswordVisible;
	private InputType loginPasswordInput = InputType.Password;
	private string loginPasswordIcon = Icons.Material.Filled.VisibilityOff;

	private string firstName = string.Empty;
	private string lastName = string.Empty;
	private string middleName = string.Empty;
	private string registerEmail = string.Empty;
	private string registerPassword = string.Empty;
	private string confirmPassword = string.Empty;
	private bool isRegisterUserValid = true;
	private string registerErrorMessage = string.Empty;

	private bool isRegisterPasswordVisible;
	private InputType registerPasswordInput = InputType.Password;
	private string registerPasswordIcon = Icons.Material.Filled.VisibilityOff;

	private bool isConfirmPasswordVisible;
	private InputType confirmPasswordInput = InputType.Password;
	private string confirmPasswordIcon = Icons.Material.Filled.VisibilityOff;

	protected override async Task OnInitializedAsync()
	{
		isRegisterMode = IsRegisterRoute();
		isForgotPasswordMode = IsForgotPasswordRoute();

		var isAuthenticated = await IAuthService.IsAuthenticated();

		if (isAuthenticated)
		{
			Navigation.NavigateTo("/");
			return;
		}

		_isLoading = false;
	}

	protected override void OnParametersSet()
	{
		var registerRoute = IsRegisterRoute();
		var forgotPasswordRoute = IsForgotPasswordRoute();
		var registerModeChanged = registerRoute != isRegisterMode;

		if (registerModeChanged || forgotPasswordRoute != isForgotPasswordMode)
		{
			isRegisterMode = registerRoute;
			isForgotPasswordMode = forgotPasswordRoute;
			hasSwitchedMode = registerModeChanged;
		}
	}

	private bool IsRegisterRoute()
		=> new Uri(Navigation.Uri)
			.AbsolutePath
			.TrimEnd('/')
			.EndsWith("/register", StringComparison.OrdinalIgnoreCase);

	private bool IsForgotPasswordRoute()
		=> new Uri(Navigation.Uri)
			.AbsolutePath
			.TrimEnd('/')
			.EndsWith("/forgot-password", StringComparison.OrdinalIgnoreCase);

	private string GetAuthCardClass()
	{
		if (isRegisterMode)
			return "auth-card active";
		if (isForgotPasswordMode)
			return "auth-card forgot";

		return hasSwitchedMode ? "auth-card close" : "auth-card";
	}

	private void ShowRegister()
	{
		hasSwitchedMode = true;
		isRegisterMode = true;
		isForgotPasswordMode = false;
		Navigation.NavigateTo("/register", replace: true);
	}

	private void ShowLogin()
	{
		hasSwitchedMode = isRegisterMode;
		isRegisterMode = false;
		isForgotPasswordMode = false;
		Navigation.NavigateTo("/login", replace: true);
	}

	private void ShowForgotPassword()
	{
		hasSwitchedMode = true;
		isRegisterMode = false;
		isForgotPasswordMode = true;
		forgotPasswordEmail = loginEmail;
		Navigation.NavigateTo("/forgot-password", replace: true);
	}

	private void ToggleLoginPasswordVisibility()
	{
		isLoginPasswordVisible = !isLoginPasswordVisible;
		loginPasswordInput = isLoginPasswordVisible ? InputType.Text : InputType.Password;
		loginPasswordIcon = isLoginPasswordVisible
			? Icons.Material.Filled.Visibility
			: Icons.Material.Filled.VisibilityOff;
	}

	private void ToggleRegisterPasswordVisibility()
	{
		isRegisterPasswordVisible = !isRegisterPasswordVisible;
		registerPasswordInput = isRegisterPasswordVisible ? InputType.Text : InputType.Password;
		registerPasswordIcon = isRegisterPasswordVisible
			? Icons.Material.Filled.Visibility
			: Icons.Material.Filled.VisibilityOff;
	}

	private void ToggleConfirmPasswordVisibility()
	{
		isConfirmPasswordVisible = !isConfirmPasswordVisible;
		confirmPasswordInput = isConfirmPasswordVisible ? InputType.Text : InputType.Password;
		confirmPasswordIcon = isConfirmPasswordVisible
			? Icons.Material.Filled.Visibility
			: Icons.Material.Filled.VisibilityOff;
	}

	private string? ValidatePassword(string password)
	{
		if (string.IsNullOrWhiteSpace(password))
			return "Password is required";

		if (password.Length < 8)
			return "Password must be at least 8 characters";

		return null;
	}

	private string? ValidateConfirmPassword(string confirmPasswordValue)
	{
		if (string.IsNullOrWhiteSpace(confirmPasswordValue))
			return "Please confirm your password";

		if (confirmPasswordValue != registerPassword)
			return "Passwords do not match";

		return null;
	}

	private async Task HandleLogin()
	{
		isLoginLoading = true;
		isLoginUserValid = true;
		loginErrorMessage = string.Empty;

		try
		{
			var userData = await IAuthService.Login(
				new LoginCred(
					loginEmail,
					loginPassword,
					rememberMe));

			if (!string.IsNullOrWhiteSpace(userData.detail))
			{
				Console.WriteLine($"Login Error: {userData.detail}");
				isLoginLoading = false;
				isLoginUserValid = false;
				loginErrorMessage = userData.detail;
				StateHasChanged();
				return;
			}

			Navigation.NavigateTo("/");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Login Exception: {ex.Message}");
			isLoginLoading = false;
			isLoginUserValid = false;
			loginErrorMessage = "An unexpected error occurred. Please try again.";
			StateHasChanged();
		}
	}

	private async Task HandleLoginEnter()
	{
		if (loginForm is null || isLoginLoading)
			return;

		await loginForm.ValidateAsync();

		if (loginForm.IsValid)
			await HandleLogin();
	}

	private async Task HandleRegister()
	{
		await registerForm!.ValidateAsync();

		isRegisterLoading = true;
		isRegisterUserValid = true;
		registerErrorMessage = string.Empty;

		try
		{
			var registerData = new RegisterRequestDTO(
				Email: registerEmail,
				PasswordHash: registerPassword,
				FirstName: firstName,
				LastName: lastName,
				MiddleName: string.IsNullOrWhiteSpace(middleName) ? null : middleName);

			var result = await IAuthService.Register(registerData);

			if (!string.IsNullOrWhiteSpace(result.errorMessage))
			{
				Console.WriteLine($"Register Error: {result.errorMessage}");
				isRegisterLoading = false;
				isRegisterUserValid = false;
				registerErrorMessage = result.errorMessage;
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
			isRegisterLoading = false;
			isRegisterUserValid = false;
			registerErrorMessage = "An unexpected error occurred. Please try again.";
			StateHasChanged();
		}
	}

	private async Task HandleForgotPassword()
	{
		isForgotPasswordLoading = true;
		isForgotPasswordUserValid = true;
		isForgotPasswordSuccess = false;
		forgotPasswordErrorMessage = string.Empty;
		forgotPasswordSuccessMessage = string.Empty;

		try
		{
			var user = await IAuthService.ForgotPasswordSendEmail(
				new SendEmailForgotPasswordRequestDTO(forgotPasswordEmail));

			if (!string.IsNullOrEmpty(user.errorMessage))
			{
				isForgotPasswordUserValid = false;
				forgotPasswordErrorMessage = user.errorMessage;
				return;
			}

			isForgotPasswordSuccess = true;
			forgotPasswordSuccessMessage = "A reset link has been sent to your email.";
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
			isForgotPasswordUserValid = false;
			forgotPasswordErrorMessage = "An unexpected error occurred. Please try again.";
		}
		finally
		{
			isForgotPasswordLoading = false;
			StateHasChanged();
		}
	}

	private async Task HandleForgotPasswordEnter()
	{
		if (forgotPasswordForm is null || isForgotPasswordLoading)
			return;

		await forgotPasswordForm.ValidateAsync();

		if (forgotPasswordForm.IsValid)
			await HandleForgotPassword();
	}
}
