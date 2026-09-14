namespace ATS.Services.Settings.EmailAccountManagement;

/// <summary>
/// Registering, verifying, editing and removing the mailboxes ATS sends invitations through.
/// </summary>
/// <remarks>
/// Every path that can change a credential goes through a code sent to the account's own mailbox,
/// through its own SMTP session. See docs/ats-email-accounts.md for why that ordering is the
/// point rather than a formality.
/// </remarks>
public interface IAtsEmailAccountManagementService
{
	/// <summary>
	/// Every registered account with its health and consumption, ordered by priority.
	/// </summary>
	/// <remarks>
	/// Not paginated, unlike the other management lists. The number of sender mailboxes is
	/// bounded by how many an operator is willing to maintain - a handful - and the table shows
	/// live health, which a cursor page would let drift between pages.
	/// </remarks>
	Task<List<EmailAccountDTO>> GetAccountsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Writes the account as Pending and sends a code through the submitted credentials.
	/// </summary>
	/// <remarks>
	/// Throws <c>BadRequestException</c> carrying the classified SMTP reason when the provider
	/// refuses the credentials, so a mistyped app password fails on the form rather than in a
	/// background job hours later.
	/// </remarks>
	Task<EmailAccountOtpSentDTO> RegisterAsync(
		RegisterEmailAccountDTO account,
		CancellationToken cancellationToken);

	/// <summary>
	/// Applies the safe fields immediately; sends a code when a credential field changed.
	/// </summary>
	/// <remarks>
	/// Returns an <see cref="EmailAccountOtpSentDTO"/> when verification is required and null
	/// when the edit is already saved, which is what tells the dialog whether to open the code
	/// step or simply close.
	/// </remarks>
	Task<EmailAccountOtpSentDTO?> EditAsync(
		EditEmailAccountDTO account,
		CancellationToken cancellationToken);

	/// <summary>
	/// Sends a code to the account's own mailbox; the row is removed only once it is confirmed
	/// through <see cref="VerifyOtpAsync"/> with a Delete purpose.
	/// </summary>
	/// <remarks>
	/// Always returns a value - unlike <see cref="EditAsync"/>, there is no version of deleting an
	/// account that skips verification, because losing a sender silently shrinks the queue's
	/// capacity and nothing else would notice until throughput dropped.
	/// </remarks>
	Task<EmailAccountOtpSentDTO> DeleteAsync(
		DeleteEmailAccountDTO request,
		CancellationToken cancellationToken);

	/// <summary>Checks a code and applies whatever it was approving.</summary>
	Task<EmailAccountOtpResultDTO> VerifyOtpAsync(
		VerifyEmailAccountOtpDTO request,
		CancellationToken cancellationToken);

	/// <summary>Invalidates outstanding codes and sends a fresh one.</summary>
	Task<EmailAccountOtpSentDTO> ResendOtpAsync(
		ResendEmailAccountOtpDTO request,
		CancellationToken cancellationToken);
}
