// ShareData.ATS is not in GlobalUsing.cs - the notification item imports it the same way.
using FrontendWebassembly.ShareData.ATS;

namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// The registry of sender mailboxes ATS sends candidate invitations through.
/// </summary>
/// <remarks>
/// Register, edit and delete all end in a code sent through the account's own SMTP session to
/// its own mailbox. This page owns that two-step sequence rather than the dialogs: the form
/// dialog closes with a DTO, this page posts it, and when the server answers with a sent code
/// it opens <see cref="VerifyEmailAccountOtpComponent"/> on top. Nothing is applied until that
/// second step succeeds - the server is what applies it, on the verify call.
///
/// See docs/ats-email-accounts.md.
/// </remarks>
public partial class EmailAccountManagement
{
	private TableComponent<EmailAccountDTO>? _accountsTable;

	private List<EmailAccountDTO> _accounts = [];
	private string _searchString = string.Empty;
	private bool _isLoading;
	private bool _isBusy;

	// Filtered in the browser rather than on the server. The list is the whole set of sender
	// mailboxes - a handful of rows - so a round trip per keystroke would buy nothing and the
	// health figures would flicker as each response landed.
	private IEnumerable<EmailAccountDTO> FilteredAccounts =>
		string.IsNullOrWhiteSpace(_searchString)
			? _accounts
			: _accounts.Where(account =>
				account.DisplayName.Contains(_searchString, StringComparison.OrdinalIgnoreCase)
				|| account.EmailAddress.Contains(_searchString, StringComparison.OrdinalIgnoreCase));

	private string EmptyTitle => _accounts.Count == 0
		? "No sender accounts"
		: "No matching accounts";

	private string EmptyMessage => _accounts.Count == 0
		? "Register a mailbox to start sending invitations through it."
		: "Nothing here matches that search.";

	private static readonly DialogOptions FormDialogOptions = new()
	{
		NoHeader = true,
		MaxWidth = MaxWidth.Small,
		FullWidth = true,
		BackdropClick = false
	};

	private static readonly DialogOptions OtpDialogOptions = new()
	{
		NoHeader = true,
		MaxWidth = MaxWidth.ExtraSmall,
		FullWidth = true,
		BackdropClick = false
	};

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		// Without this guard the RequirePermission/RequireATSModule attributes are inert.
		if (!IsPageAuthorized)
		{
			return;
		}

		await LoadAccountsAsync();
	}

	private async Task LoadAccountsAsync()
	{
		if (_isLoading)
		{
			return;
		}

		_isLoading = true;

		try
		{
			var response = await EmailAccountService.GetAccountsAsync();

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			_accounts = response.Data;
		}
		finally
		{
			_isLoading = false;
		}
	}

	private void OnSearchChanged(string value) => _searchString = value;

	// ---------- Register ----------

	private async Task AddEmailAccountAsync()
	{
		var dialog = await DialogService.ShowAsync<AddEmailAccountComponent>(
			"Register sender email", FormDialogOptions);

		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not RegisterEmailAccountDTO account)
		{
			return;
		}

		_isBusy = true;

		try
		{
			var response = await EmailAccountService.RegisterAsync(account);

			if (!response.IsSuccess || response.Data is null)
			{
				// Carries the provider's own refusal when the credentials were rejected, which
				// is the whole point of sending the code before the account is trusted.
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			// The row exists but is Pending and invisible to the sender until the code lands,
			// so reloading now is honest about what happened even if verification is abandoned.
			await LoadAccountsAsync();

			var verified = await ConfirmWithOtpAsync(response.Data);

			if (verified)
			{
				Snackbar.Add($"{account.EmailAddress} is verified and in rotation.", Severity.Success);
				await LoadAccountsAsync();
			}
			else
			{
				Snackbar.Add(
					$"{account.EmailAddress} was saved but is not verified, so it will not send yet.",
					Severity.Warning);
			}
		}
		finally
		{
			_isBusy = false;
		}
	}

	// ---------- Edit ----------

	private async Task EditEmailAccountAsync(EmailAccountDTO account)
	{
		var parameters = new DialogParameters<EditEmailAccountComponent>
		{
			{ component => component.Account, account }
		};

		var dialog = await DialogService.ShowAsync<EditEmailAccountComponent>(
			"Edit sender email", parameters, FormDialogOptions);

		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not EditEmailAccountDTO edit)
		{
			return;
		}

		_isBusy = true;

		try
		{
			var response = await EmailAccountService.EditAsync(edit);

			if (!response.IsSuccess)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			await LoadAccountsAsync();

			// A null payload is the success case for an edit that touched no credential field:
			// it is already saved and there is nothing to confirm.
			if (response.Data is null)
			{
				Snackbar.Add("Account updated.", Severity.Success);
				return;
			}

			var verified = await ConfirmWithOtpAsync(response.Data);

			if (verified)
			{
				Snackbar.Add("Account updated and verified.", Severity.Success);
				await LoadAccountsAsync();
			}
			else
			{
				Snackbar.Add(
					"The new credentials are not verified, so this account stays out of rotation.",
					Severity.Warning);
			}
		}
		finally
		{
			_isBusy = false;
		}
	}

	// ---------- Delete ----------

	private async Task DeleteEmailAccountAsync(EmailAccountDTO account)
	{
		var confirmed = await ConfirmActionAsync(
			"Delete sender account",
			$"Removing {account.EmailAddress} takes {account.DailySendLimit} messages a day out of "
			+ "your sending capacity, and the invitations it would have carried move onto the "
			+ "remaining accounts. We will email a code to this mailbox to confirm.",
			"Send code");

		if (!confirmed)
		{
			return;
		}

		_isBusy = true;

		try
		{
			var response = await EmailAccountService.DeleteAsync(
				new DeleteEmailAccountDTO { AtsEmailAccountId = account.AtsEmailAccountId });

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			var verified = await ConfirmWithOtpAsync(response.Data);

			if (verified)
			{
				Snackbar.Add($"{account.EmailAddress} was removed.", Severity.Success);
				await LoadAccountsAsync();
			}
			else
			{
				// Nothing was removed - the deletion only happens on the server when the code
				// is accepted - so this is a cancellation, not a failure.
				Snackbar.Add("Deletion cancelled. The account is unchanged.", Severity.Info);
			}
		}
		finally
		{
			_isBusy = false;
		}
	}

	/// <summary>
	/// Opens the code dialog for a sent code and reports whether it was confirmed.
	/// </summary>
	private async Task<bool> ConfirmWithOtpAsync(EmailAccountOtpSentDTO otpSent)
	{
		var parameters = new DialogParameters<VerifyEmailAccountOtpComponent>
		{
			{ component => component.OtpSent, otpSent }
		};

		var dialog = await DialogService.ShowAsync<VerifyEmailAccountOtpComponent>(
			"Verify code", parameters, OtpDialogOptions);

		var result = await dialog.Result;

		return result is { Canceled: false, Data: EmailAccountOtpResultDTO { IsVerified: true } };
	}

	// ---------- Presentation ----------

	/// <summary>
	/// The single most useful thing about the account's current state.
	/// </summary>
	/// <remarks>
	/// Ordered by what blocks sending soonest, not by severity: an account can be unverified
	/// AND disabled AND cooling down at once, and showing the condition nearest the top of the
	/// selector's checks is what tells the operator which one to fix first.
	/// </remarks>
	private static string StatusLabel(EmailAccountDTO account)
	{
		if (account.VerificationStatus == AtsEmailAccountStatuses.NeedsReverification)
		{
			return "Needs re-verification";
		}

		if (account.VerificationStatus == AtsEmailAccountStatuses.Pending)
		{
			return "Pending verification";
		}

		if (!account.IsActive)
		{
			return "Disabled";
		}

		if (account.CoolingDownUntil.HasValue && account.CoolingDownUntil.Value > DateTime.UtcNow)
		{
			return "Cooling down";
		}

		if (account.RemainingInWindow <= 0)
		{
			return "Daily cap reached";
		}

		return account.IsSendable ? "Active" : "Not sending";
	}

	private static string StatusModifier(EmailAccountDTO account)
	{
		if (account.VerificationStatus == AtsEmailAccountStatuses.NeedsReverification)
		{
			return "error";
		}

		if (account.VerificationStatus == AtsEmailAccountStatuses.Pending)
		{
			return "pending";
		}

		if (!account.IsActive)
		{
			return "unknown";
		}

		if (account.CoolingDownUntil.HasValue && account.CoolingDownUntil.Value > DateTime.UtcNow)
		{
			return "processing";
		}

		if (account.RemainingInWindow <= 0)
		{
			return "processing";
		}

		return account.IsSendable ? "done" : "unknown";
	}

	private static double ConsumptionPercent(EmailAccountDTO account)
	{
		if (account.DailySendLimit <= 0)
		{
			return 0;
		}

		var percent = account.ConsumedInWindow * 100d / account.DailySendLimit;

		// Clamped because the window count can momentarily exceed a limit that was just lowered,
		// and a bar wider than its track reads as a rendering fault rather than as "over cap".
		return Math.Clamp(percent, 0, 100);
	}

	private static string ConsumptionCssClass(EmailAccountDTO account)
	{
		var percent = ConsumptionPercent(account);

		return percent >= 100
			? "ats-email-consumption-bar is-full"
			: percent >= 80
				? "ats-email-consumption-bar is-high"
				: "ats-email-consumption-bar";
	}

	private static string EditTooltip(EmailAccountDTO account) => account.IsInUse
		? "A send is in flight through this account. Try again in a moment."
		: "Edit this account";

	private static string DeleteTooltip(EmailAccountDTO account) => account.IsInUse
		? "A send is in flight through this account. Try again in a moment."
		: "Delete this account";

	private static string FormatTimestamp(DateTime? value) => value.HasValue
		? value.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
		: "Never";
}
