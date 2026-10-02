namespace FrontendWebassembly.Services.ATS.EmailAccountManagement;

/// <summary>
/// The sender mailboxes ATS sends candidate invitations through.
/// </summary>
/// <remarks>
/// Register, edit and delete all end in a code sent to the account's own mailbox through its own
/// SMTP session, confirmed via <see cref="VerifyOtpAsync"/>. See docs/ats-email-accounts.md.
/// </remarks>
public interface IAtsEmailAccountService
{
	/// <summary>
	/// Every registered account with its live health and rolling-24h consumption.
	/// </summary>
	/// <remarks>
	/// Returns the whole list rather than a page: there are only ever a handful of sender
	/// mailboxes, and paging live health would let the figures drift between pages.
	/// </remarks>
	Task<ServiceResponse<List<EmailAccountDTO>>> GetAccountsAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Writes the account as unverified and sends a code through the submitted credentials.
	/// </summary>
	/// <remarks>
	/// Fails with the provider's own reason when the credentials are refused - a mistyped app
	/// password surfaces on this form rather than silently stalling the queue later.
	/// </remarks>
	Task<ServiceResponse<EmailAccountOtpSentDTO>> RegisterAsync(
		RegisterEmailAccountDTO account,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves the edit, or sends a code when a credential field changed.
	/// </summary>
	/// <remarks>
	/// <c>Data</c> is null when the edit already saved and holds the sent-code details when
	/// verification is needed - which is what tells the page whether to open the code dialog.
	/// </remarks>
	Task<ServiceResponse<EmailAccountOtpSentDTO?>> EditAsync(
		EditEmailAccountDTO account,
		CancellationToken cancellationToken = default);

	/// <summary>Sends a code confirming a deletion. Removes nothing on its own.</summary>
	Task<ServiceResponse<EmailAccountOtpSentDTO>> DeleteAsync(
		DeleteEmailAccountDTO request,
		CancellationToken cancellationToken = default);

	/// <summary>Submits a code, applying whatever it was approving.</summary>
	Task<ServiceResponse<EmailAccountOtpResultDTO>> VerifyOtpAsync(
		VerifyEmailAccountOtpDTO request,
		CancellationToken cancellationToken = default);

	/// <summary>Invalidates outstanding codes and sends a fresh one.</summary>
	Task<ServiceResponse<EmailAccountOtpSentDTO>> ResendOtpAsync(
		ResendEmailAccountOtpDTO request,
		CancellationToken cancellationToken = default);
}
