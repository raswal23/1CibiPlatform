namespace FrontendWebassembly.Pages.Auth;

public partial class Otp
{
	public string? email = "";
	public string? userId = "";

	private MudForm? form;
	private bool success;
	private bool isLoading = false;
	private bool isResending = false;
	private bool _isLoading = true;
	private bool isDone => showSuccessMessage;
	private bool isResendLoading = false;
	private bool isResendSuccess = false;
	private bool hasUnsavedChanges = true;

	private string otpCode = "";
	private bool isOtpValid = true;
	private string errorMessage = "";
	private bool showResendSuccess = false;

	private Timer? countdownTimer;
	private bool isNavigationLocked = false;

	private bool showSuccessMessage = false;
	private int redirectCountdown = 5;

	protected override async Task OnInitializedAsync()
	{
		userId = await LocalStorageService.GetItemAsync<string>("tempUserId");
		email = await LocalStorageService.GetItemAsync<string>("tempUserEmail");

		// Check if email parameter exists
		if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(userId))
		{
			Navigation.NavigateTo("/register");
			return;
		}

		var userOtpDetails = new OtpSessionRequestDTO(userId!, email!);

		var isUserOtpValidated = await IAuthService.IsOtpSessionValid(userOtpDetails);

		if (!isUserOtpValidated.isValid)
		{
			await LocalStorageService.RemoveItemAsync("tempUserId");
			await LocalStorageService.RemoveItemAsync("tempUserEmail");
			Navigation.NavigateTo("/login");
			return;
		}

		_isLoading = false;
	}

	private async Task ConfirmNavigation(LocationChangingContext context)
	{
		if (hasUnsavedChanges)
		{
			var result = await JSRuntime.InvokeAsync<bool>("confirm", "You have unsaved changes. Leave anyway?");

			if (!result)
			{
				context.PreventNavigation();
			}
		}
	}

	private string? ValidateOtp(string otp)
	{
		if (string.IsNullOrWhiteSpace(otp))
			return "Verification code is required";

		if (otp.Length != 6)
			return "Verification code must be 6 characters";

		return null;
	}

	private async Task HandleVerifyOtp()
	{
		SetState();

		try
		{
			var otpDetails = new OtpVerificationRequestDTO(email!, otpCode);

			var result = await IAuthService.OtpVerification(otpDetails);

			if (!result.isValid)
			{
				Console.WriteLine($"OTP Verification Error: {result.errorMessage}");
				isLoading = false;
				isOtpValid = false;
				isResending = false;
				isResendLoading = false;
				errorMessage = result.errorMessage;
				StateHasChanged();
				return;
			}

			// Show success message and start countdown
			isLoading = false;
			isResendLoading = false;
			showSuccessMessage = true;
			await LocalStorageService.RemoveItemAsync("tempUserId");
			await LocalStorageService.RemoveItemAsync("tempUserEmail");
			StateHasChanged();

			// Start countdown timer
			countdownTimer = new Timer(async _ =>
			{
				redirectCountdown--;

				if (redirectCountdown <= 0)
				{
					countdownTimer?.Dispose();
					await InvokeAsync(() =>
					{
						hasUnsavedChanges = false;
						Navigation.NavigateTo("/login", false);
					});
				}
				else
				{
					await InvokeAsync(StateHasChanged);
				}
			}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
		}
		catch (Exception ex)
		{
			Console.WriteLine($"OTP Verification Exception: {ex.Message}");
			isLoading = false;
			isResendLoading = false;
			isOtpValid = false;
			errorMessage = "An unexpected error occurred. Please try again.";
			StateHasChanged();
		}
	}

	private async void HandleResendOtp()
	{
		SetState();

		try
		{
			var otpDetails = new OTPResendRequestDTO(Guid.Parse(userId!), email!);
			var result = await IAuthService.OtpResendAsync(otpDetails);
			if (!result.isSuccess)
			{
				Console.WriteLine($"Resend OTP Error: {result.errorMessage}");
				isLoading = false;
				isOtpValid = false;
				isResendLoading = false;
				errorMessage = result.errorMessage;
				StateHasChanged();
				return;
			}
			isLoading = false;
			isResendLoading = false;
			showResendSuccess = true;
			isResendSuccess = true;
			StateHasChanged();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Resend OTP Exception: {ex.Message}");
			isLoading = false;
			isOtpValid = false;
			isResendLoading = false;
			errorMessage = "An unexpected error occurred. Please try again.";
			StateHasChanged();
		}
	}

	private void SetState()
	{
		isLoading = true;
		isOtpValid = true;
		isResendLoading = true;
		errorMessage = "";
		showResendSuccess = true;
		isResendSuccess = false;
	}
}
