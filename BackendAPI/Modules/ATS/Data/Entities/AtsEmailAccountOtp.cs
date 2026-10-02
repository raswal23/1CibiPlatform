namespace ATS.Data.Entities;

/// <summary>
/// A one-time code sent to a sender account's own mailbox, through that account's own SMTP
/// credentials, to prove the credentials work and the mailbox is ours before the account is
/// allowed to send anything.
/// </summary>
/// <remarks>
/// ATS cannot reuse Auth's <c>OtpVerification</c> table: the two modules are separate
/// DbContexts against separate schemas, so that entity is not reachable from here. This is its
/// shape minus the registration fields, and it stores the same hash rather than the code - an
/// OTP readable in the database is not a second factor.
///
/// The code proves two things at once. A send that connects and authenticates proves the app
/// password is right; the code arriving in that same mailbox proves whoever registered it can
/// read it. Doing this at registration rather than at first send is the whole point: a typo'd
/// password otherwise surfaces as a queue that has silently stopped.
///
/// Rows outlive their use so a replayed code can be recognised as already consumed rather than
/// simply missing, which would be indistinguishable from an expired one.
/// </remarks>
public sealed class AtsEmailAccountOtp
{
	public long AtsEmailAccountOtpId { get; set; }

	public int AtsEmailAccountId { get; set; }

	// AtsEmailAccountOtpPurpose. Register, Edit and Delete each mint their own code, so
	// approving a password change can never be replayed to approve a deletion.
	public string Purpose { get; set; } = string.Empty;

	public string OtpCodeHash { get; set; } = string.Empty;

	// Capped at AtsEmailAccountOtpPolicy.MaxAttempts, mirroring Auth's RegisterService. Without
	// a cap a six-digit code is a million cheap guesses.
	public int AttemptCount { get; set; }

	// Set on success AND on running out of attempts, so a consumed code cannot be retried.
	public bool IsUsed { get; set; }

	/// <summary>
	/// The pending change this code approves, as JSON, for Edit only.
	/// </summary>
	/// <remarks>
	/// Held here rather than applied to the account row because an unverified credential change
	/// must not reach the selector: writing the new password to the account first would put an
	/// unproven credential into rotation the moment anything cleared the status. Null for
	/// Register (the row itself is the pending state) and for Delete (there is nothing to hold).
	/// Contains a protected password, so it is masked by AtsAuditRedactor like any other.
	/// </remarks>
	public string? PendingChangesJson { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime ExpiresAt { get; set; }

	public DateTime? VerifiedAt { get; set; }
}
