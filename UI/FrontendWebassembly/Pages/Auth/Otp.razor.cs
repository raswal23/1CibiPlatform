using Microsoft.AspNetCore.Components.Routing;

namespace FrontendWebassembly.Pages.Auth;

public partial class Otp
{
	public string? email = "";
	public string? userId = "";

	private MudForm form;
	private bool success;
	private bool isLoading = false;
	private bool isResending = false;
	private bool _isLoading = true;
	private bool isDone => showSuccessMessage;
	private bool isResendLoading = false;
	private bool isResendSuccess = false;

	private string otpCode = "";
	private bool isOtpValid = true;
	private string errorMessage = "";
	private bool showResendSuccess = false;

	private System.Threading.Timer? countdownTimer;
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
			Navigation.NavigateTo("/register", true);
			return;
		}

		var userOtpDetails = new OtpSessionRequestDTO(userId!, email!);

		var isUserOtpValidated = await IAuthService.IsOtpSessionValid(userOtpDetails);

		if (!isUserOtpValidated.isValid)
		{
			await LocalStorageService.RemoveItemAsync("tempUserId");
			await LocalStorageService.RemoveItemAsync("tempUserEmail");
			Navigation.NavigateTo("/login", true);
			return;
		}

		_isLoading = false;

		Navigation.LocationChanged += HandleLocationChanged!;
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await SetupNavigationLock();
		}
	}

	private async Task SetupNavigationLock()
	{
		if (!isNavigationLocked)
		{
			isNavigationLocked = true;
			await JSRuntime.InvokeVoidAsync(
			"navigationLock.enable",
			"You have an unverified account. Are you sure you want to leave?"
			);
		}
	}

	private async Task RemoveNavigationLock()
	{
		if (isNavigationLocked)
		{
			isNavigationLocked = false;
			await JSRuntime.InvokeVoidAsync("navigationLock.disable");
		}
	}

	private void HandleLocationChanged(object sender, LocationChangedEventArgs e)
	{
		if (!e.Location.Contains("/verify-otp"))
		{
			var confirmed = JSRuntime.InvokeAsync<bool>("confirm",
				"You have an unverified account. Are you sure you want to leave?").GetAwaiter().GetResult();

			if (!confirmed)
			{
				Navigation.NavigateTo($"/verify-otp?userId={userId}&email={email}", false);
			}
			else
			{
				RemoveNavigationLock().GetAwaiter().GetResult();
			}
		}
	}

	private string ValidateOtp(string otp)
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

			await RemoveNavigationLock();

			// Show success message and start countdown
			isLoading = false;
			isResendLoading = false;
			showSuccessMessage = true;
			await LocalStorageService.RemoveItemAsync("tempUserId");
			await LocalStorageService.RemoveItemAsync("tempUserEmail");
			StateHasChanged();

			// Start countdown timer
			countdownTimer = new System.Threading.Timer(async _ =>
			{
				redirectCountdown--;

				if (redirectCountdown <= 0)
				{
					countdownTimer?.Dispose();
					await InvokeAsync(() =>
					{

						Navigation.NavigateTo("/login", true);
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

	public async ValueTask DisposeAsync()
	{
		Navigation.LocationChanged -= HandleLocationChanged;
		await RemoveNavigationLock();
	}
}
