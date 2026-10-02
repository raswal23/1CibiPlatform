namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// The code step shared by registering, editing and deleting a sender account.
/// </summary>
/// <remarks>
/// One dialog for all three purposes rather than three near-identical ones: the code, the
/// attempt cap and the resend behave identically whatever is being approved, and only the
/// wording changes. The purpose travels on <see cref="EmailAccountOtpSentDTO.Purpose"/>, which
/// the server also checks - this component never decides what a code is allowed to do.
/// </remarks>
public partial class VerifyEmailAccountOtpComponent
{
	[Inject] private IAtsEmailAccountService EmailAccountService { get; set; } = default!;

	[CascadingParameter] private IMudDialogInstance? Dialog { get; set; }

	/// <summary>Where the code went, echoed back from the call that sent it.</summary>
	[Parameter] public EmailAccountOtpSentDTO OtpSent { get; set; } = new();

	private readonly string[] otpDigits = Enumerable.Repeat(string.Empty, 6).ToArray();
	private readonly ElementReference[] otpInputs = new ElementReference[6];

	private bool isBusy;
	private bool isResendSuccess;
	private string errorMessage = string.Empty;

	// Null until the server has actually counted an attempt, so the dialog does not open
	// announcing "5 attempts left" before anything has been tried.
	private int? remainingAttempts;

	private bool IsOtpComplete => otpDigits.All(digit => !string.IsNullOrEmpty(digit));

	private string OtpBoxesClass => string.IsNullOrWhiteSpace(errorMessage)
		? "eo-boxes"
		: "eo-boxes error";

	private string HeaderTitle => OtpSent.Purpose switch
	{
		EmailAccountOtpPurposes.Register => "Verify sender email",
		EmailAccountOtpPurposes.Edit => "Confirm credential change",
		EmailAccountOtpPurposes.Delete => "Confirm removal",
		_ => "Verify code"
	};

	private string HeaderSubtitle => OtpSent.Purpose switch
	{
		EmailAccountOtpPurposes.Register => "Prove the mailbox and password work",
		EmailAccountOtpPurposes.Edit => "The new credentials sent this code",
		EmailAccountOtpPurposes.Delete => "Removing this account shrinks your sending capacity",
		_ => "Enter the code we sent"
	};

	private string ConfirmLabel => OtpSent.Purpose == EmailAccountOtpPurposes.Delete
		? "Delete account"
		: "Verify";

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await otpInputs[0].FocusAsync();
		}
	}

	private async Task UpdateOtpDigitAsync(int index, ChangeEventArgs args)
	{
		var value = args.Value?.ToString() ?? string.Empty;

		// Takes the last digit typed rather than the whole value: the box is maxlength 1, but
		// a paste or an IME can still deliver several characters at once.
		var digit = value.LastOrDefault(char.IsDigit);
		otpDigits[index] = digit == default ? string.Empty : digit.ToString();

		// A fresh code clears the previous refusal, otherwise the error outlives what caused it.
		errorMessage = string.Empty;

		if (!string.IsNullOrEmpty(otpDigits[index]) && index < otpDigits.Length - 1)
		{
			await otpInputs[index + 1].FocusAsync();
		}
	}

	private async Task HandleOtpKeyDownAsync(int index, KeyboardEventArgs args)
	{
		if (args.Key == "Enter")
		{
			if (IsOtpComplete && !isBusy)
			{
				await VerifyAsync();
			}

			return;
		}

		// Backspace on an already-empty box steps back, so holding it clears the row.
		if (args.Key == "Backspace" && string.IsNullOrEmpty(otpDigits[index]) && index > 0)
		{
			await otpInputs[index - 1].FocusAsync();
		}
	}

	private async Task VerifyAsync()
	{
		if (isBusy || !IsOtpComplete)
		{
			return;
		}

		isBusy = true;
		isResendSuccess = false;
		errorMessage = string.Empty;

		try
		{
			var response = await EmailAccountService.VerifyOtpAsync(new VerifyEmailAccountOtpDTO
			{
				AtsEmailAccountId = OtpSent.AtsEmailAccountId,
				Purpose = OtpSent.Purpose,
				OtpCode = string.Concat(otpDigits)
			});

			if (!response.IsSuccess || response.Data is null)
			{
				errorMessage = response.ErrorDetail;
				return;
			}

			var result = response.Data;

			if (!result.IsVerified)
			{
				// A wrong code is a successful call with a negative verdict, so the remaining
				// attempts come back with it and the dialog stays open to show them.
				errorMessage = result.Message ?? "That code is not correct.";
				remainingAttempts = result.RemainingAttempts;
				ClearDigits();
				await otpInputs[0].FocusAsync();
				return;
			}

			Dialog!.Close(DialogResult.Ok(result));
		}
		finally
		{
			isBusy = false;
		}
	}

	private async Task ResendAsync()
	{
		if (isBusy)
		{
			return;
		}

		isBusy = true;
		errorMessage = string.Empty;
		isResendSuccess = false;

		try
		{
			var response = await EmailAccountService.ResendOtpAsync(new ResendEmailAccountOtpDTO
			{
				AtsEmailAccountId = OtpSent.AtsEmailAccountId,
				Purpose = OtpSent.Purpose
			});

			if (!response.IsSuccess || response.Data is null)
			{
				errorMessage = response.ErrorDetail;
				return;
			}

			// The new code has its own expiry and its own attempt budget; both are replaced
			// rather than carried over, so the footer and the counter must follow it.
			OtpSent = response.Data;
			remainingAttempts = null;
			isResendSuccess = true;
			ClearDigits();
			await otpInputs[0].FocusAsync();
		}
		finally
		{
			isBusy = false;
		}
	}

	private void ClearDigits()
	{
		for (var index = 0; index < otpDigits.Length; index++)
		{
			otpDigits[index] = string.Empty;
		}
	}

	private void Cancel() => Dialog!.Cancel();
}
