namespace FrontendWebassembly.Pages.Auth;

public partial class Login
{
	private MudForm form;
	private bool success;
	private bool isLoading = false;
	private bool _isLoading = true;

	private string email = "";
	private string password = "";
	private bool rememberMe = false;
	private bool isUserValid = true;
	private string errorMessage = "";

	private bool isPasswordVisible = false;
	private InputType passwordInput = InputType.Password;
	private string passwordIcon = Icons.Material.Filled.VisibilityOff;

	protected override async Task OnInitializedAsync()
	{
		var isAuthenticated = await IAuthService.IsAuthenticated();

		if (isAuthenticated)
		{
			Navigation.NavigateTo("/", true);
			return;
		}

		var emailFromStorage = await LocalStorageService.GetItemAsync<string>("tempUserEmail");
		var userIdFromStorage = await LocalStorageService.GetItemAsync<string>("tempUserId");

		if (await LocalStorageService.GetItemAsync<string>("tempUserEmail") is not null)
		{
			Navigation.NavigateTo($"/verify-otp?userId={userIdFromStorage}&email={emailFromStorage}", true);
		}

		_isLoading = false;
	}

	private void TogglePasswordVisibility()
	{
		isPasswordVisible = !isPasswordVisible;
		passwordInput = isPasswordVisible ? InputType.Text : InputType.Password;
		passwordIcon = isPasswordVisible ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;
	}

	private async Task HandleLogin()
	{
		isLoading = true;
		isUserValid = true;
		errorMessage = "";

		try
		{
			var userData = await IAuthService.Login(new LoginCred(email, password, rememberMe));

			if (!string.IsNullOrWhiteSpace(userData.detail))
			{
				Console.WriteLine($"Login Error: {userData.detail}");
				isLoading = false;
				isUserValid = false;
				errorMessage = userData.detail;
				StateHasChanged();
				return;
			}

			Navigation.NavigateTo("/", true);
		}

		catch (Exception ex)
		{
			Console.WriteLine($"Login Exception: {ex.Message}");
			isLoading = false;
			isUserValid = false;
			errorMessage = "An unexpected error occurred. Please try again.";
			StateHasChanged();
			return;
		}
	}
}
