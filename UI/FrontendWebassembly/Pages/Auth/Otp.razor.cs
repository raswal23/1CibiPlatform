namespace FrontendWebassembly.Pages.Auth;

public partial class Otp
{
	public string? email = "";
	public string? userId = "";

	private bool isLoading = false;
	private bool isResending = false;
	private bool _isLoading = true;
	private bool isDone => showSuccessMessage;
	private bool isResendLoading = false;
	private bool isResendSuccess = false;
	private bool hasUnsavedChanges = true;

	private string otpCode = "";
	private readonly string[] otpDigits = Enumerable
		.Repeat(string.Empty, 6)
		.ToArray();
	private ElementReference otpInput1;
	private ElementReference otpInput2;
	private ElementReference otpInput3;
	private ElementReference otpInput4;
	private ElementReference otpInput5;
	private ElementReference otpInput6;
	private bool isOtpValid = true;
	private string errorMessage = "";
	private bool showResendSuccess = false;

	private Timer? countdownTimer;
	private bool isNavigationLocked = false;

	private bool showSuccessMessage = false;
	private int redirectCountdown = 5;
	private bool IsOtpComplete => otpDigits.All(digit => !string.IsNullOrEmpty(digit));

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

	private string GetOtpBoxesClass()
	{
		if (showSuccessMessage)
			return "otp-boxes success";

		return isOtpValid ? "otp-boxes" : "otp-boxes error";
	}

	private string GetVerifyButtonClass()
		=> IsOtpComplete
			? "otp-verify-button active"
			: "otp-verify-button";

	private async Task UpdateOtpDigitAsync(int index, ChangeEventArgs args)
	{
		var value = args.Value?.ToString() ?? string.Empty;
		var digit = value.LastOrDefault(char.IsDigit);
		otpDigits[index] = digit == default ? string.Empty : digit.ToString();
		otpCode = string.Concat(otpDigits);

		if (!string.IsNullOrEmpty(otpDigits[index]) && index < otpDigits.Length - 1)
			await GetOtpInput(index + 1).FocusAsync();
	}

	private async Task HandleOtpKeyDownAsync(int index, KeyboardEventArgs args)
	{
		if (args.Key == "Enter")
		{
			if (IsOtpComplete && !isLoading && !isDone)
				await HandleVerifyOtp();

			return;
		}

		if (args.Key == "Backspace"
			&& string.IsNullOrEmpty(otpDigits[index])
			&& index > 0)
		{
			await GetOtpInput(index - 1).FocusAsync();
		}
	}

	private ElementReference GetOtpInput(int index)
		=> index switch
		{
			0 => otpInput1,
			1 => otpInput2,
			2 => otpInput3,
			3 => otpInput4,
			4 => otpInput5,
			_ => otpInput6
		};

	private async Task HandleVerifyOtp()
	{
		SetState(false);

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
		SetState(true);

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

	private void SetState(bool isResendRequest)
	{
		isLoading = true;
		isOtpValid = true;
		isResendLoading = isResendRequest;
		errorMessage = "";
		showResendSuccess = true;
		isResendSuccess = false;
	}
}
