namespace ATS.Data.Repository.EmailAccounts;

/// <summary>
/// Persistence for the registry of sender accounts, their health and their consumption.
/// </summary>
/// <remarks>
/// Deliberately NOT cached, and no <c>ATSCacheRepository</c> decorator - the same call this
/// makes explicitly that <c>AtsNotificationRepository</c> and <c>AtsAuditRepository</c> make.
/// Health and consumption change on every single send, and this is read on the send hot path
/// to decide where the next message goes. A cache here would route messages to an account that
/// is already capped or already cooling down, which is the one failure the whole feature
/// exists to prevent.
///
/// <see cref="GetSnapshotsAsync"/> returns <c>AtsEmailAccountSnapshot</c> rather than the
/// entity: no caller outside this repository and the registry ever holds an object carrying a
/// password.
/// </remarks>
public interface IAtsEmailAccountRepository
{
	/// <summary>
	/// Every account with its rolling-window consumption, ordered by priority.
	/// </summary>
	/// <remarks>
	/// Returns ALL accounts rather than only the sendable ones, because the same read serves
	/// the management table - which has to show a disabled or capped account precisely so
	/// somebody can see why nothing is going out.
	/// </remarks>
	Task<List<AtsEmailAccountSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken);

	Task<AtsEmailAccountSnapshot?> GetSnapshotAsync(
		int accountId,
		CancellationToken cancellationToken);

	/// <summary>
	/// The account row itself, tracked, INCLUDING its protected password. Only the registry
	/// (to open a session) and the management service (to edit) may call this.
	/// </summary>
	Task<AtsEmailAccount?> GetAccountAsync(int accountId, CancellationToken cancellationToken);

	Task<AtsEmailAccount?> GetAccountByEmailAsync(
		string emailAddress,
		CancellationToken cancellationToken);

	Task<bool> EmailAddressExistsAsync(
		string emailAddress,
		int? excludingAccountId,
		CancellationToken cancellationToken);

	Task<bool> PriorityExistsAsync(
		int priority,
		int? excludingAccountId,
		CancellationToken cancellationToken);

	/// <summary>The lowest unused priority, so a new account lands after the existing ones.</summary>
	Task<int> GetNextAvailablePriorityAsync(CancellationToken cancellationToken);

	Task AddAsync(AtsEmailAccount account, CancellationToken cancellationToken);

	Task UpdateAsync(AtsEmailAccount account, CancellationToken cancellationToken);

	Task DeleteAsync(AtsEmailAccount account, CancellationToken cancellationToken);

	/// <summary>
	/// Appends one successful send and advances <c>LastSentAt</c>, resetting the breaker.
	/// </summary>
	/// <remarks>
	/// One call rather than two so the log row and the reset cannot diverge: an account whose
	/// consumption advanced but whose failure count did not reset would be retired while it
	/// was demonstrably working.
	/// </remarks>
	Task RecordSuccessfulSendAsync(
		int accountId,
		int recipientCount,
		DateTime sentAtUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// Writes breaker state through to the row so a restart cannot resurrect an account the
	/// provider is still throttling.
	/// </summary>
	Task RecordHealthAsync(
		int accountId,
		int consecutiveFailureCount,
		DateTime? coolingDownUntil,
		string? lastFailureReason,
		string? verificationStatus,
		CancellationToken cancellationToken);

	/// <summary>Recipients sent through one account inside the rolling window.</summary>
	Task<int> GetConsumedInWindowAsync(
		int accountId,
		DateTime windowStartUtc,
		CancellationToken cancellationToken);

	/// <summary>Drops send-log rows older than the cutoff. Returns how many went.</summary>
	Task<int> DeleteSendLogsOlderThanAsync(
		DateTime cutoffUtc,
		CancellationToken cancellationToken);

	#region One-time codes
	Task AddOtpAsync(AtsEmailAccountOtp otp, CancellationToken cancellationToken);

	/// <summary>The newest unused, unexpired code for one account and purpose.</summary>
	Task<AtsEmailAccountOtp?> GetActiveOtpAsync(
		int accountId,
		string purpose,
		DateTime asOfUtc,
		CancellationToken cancellationToken);

	Task UpdateOtpAsync(AtsEmailAccountOtp otp, CancellationToken cancellationToken);

	/// <summary>
	/// Consumes every outstanding code for one account and purpose.
	/// </summary>
	/// <remarks>
	/// Called before minting a new one. Without it, a resend leaves the previous code valid,
	/// so an operator who requested three codes would have three working at once and the
	/// attempt cap would apply to each separately rather than to the verification.
	/// </remarks>
	Task InvalidateOtpsAsync(
		int accountId,
		string purpose,
		CancellationToken cancellationToken);
	#endregion
}
