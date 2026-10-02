namespace ATS.Data.Entities;

/// <summary>
/// One SMTP sender account the ATS invitation queue may send through.
/// </summary>
/// <remarks>
/// ATS used to send through a single Gmail account whose credentials lived in .env. That
/// mailbox is capped at roughly 500 recipients a day, so growing volume meant the queue simply
/// stopped - see docs/ats-email-delivery.md for the two incidents this came out of. Registering
/// senders as rows lets the sender move to the next account when one is capped, throttled or
/// failing, which is the only way to raise a ceiling that belongs to the provider.
///
/// The row carries both the credentials and the health state the selector reads on every
/// message. Health is kept here rather than only in memory because a process restart must not
/// resurrect an account the provider is still throttling, and the management table has to show
/// health without reaching into process memory.
///
/// Send counts are deliberately NOT a column: see <see cref="AtsEmailSendLog"/>.
/// </remarks>
public sealed class AtsEmailAccount
{
	public int AtsEmailAccountId { get; set; }

	// Shown in the table and used as the From display name on messages sent through this
	// account, so a candidate sees "CIBI Recruitment" rather than a raw mailbox address.
	public string DisplayName { get; set; } = string.Empty;

	// The mailbox itself. Unique: two rows for one mailbox would share a provider quota that
	// the selector counts separately, so it would send twice as much as it believes it has.
	public string EmailAddress { get; set; } = string.Empty;

	public string SmtpHost { get; set; } = string.Empty;

	public int SmtpPort { get; set; }

	/// <summary>
	/// The app password, protected by <c>ISecretProtector</c> with this row's id as context.
	/// </summary>
	/// <remarks>
	/// Never leaves the backend: no DTO carries it in any form, and AtsAuditRedactor masks the
	/// property by name. Two-way rather than hashed because the plaintext has to be handed to
	/// the SMTP server at AUTH time. Rotating Security:SecretProtectionKey makes every value
	/// here unreadable - the passwords must then be re-entered and re-verified.
	/// </remarks>
	public string EncryptedPassword { get; set; } = string.Empty;

	// Lower wins. Unique, so "the next account" is never ambiguous - two accounts sharing a
	// priority would make selection depend on row order, and a failover would be untestable.
	public int Priority { get; set; }

	// Manual disable. Distinct from cooling down: this one is an operator's decision and never
	// expires on its own.
	public bool IsActive { get; set; }

	// The rolling-24h ceiling the selector compares the send log against. Per-account because a
	// Google Workspace mailbox allows roughly 2000 recipients a day and a consumer gmail.com
	// account roughly 500, and both may be registered in the same table.
	//
	// Set below the provider's real figure on purpose: the point is to move to the next account
	// BEFORE the provider refuses, because a refusal locks the mailbox for about 24 hours.
	public int DailySendLimit { get; set; }

	// AtsEmailAccountStatus, stored as its string name rather than an int so a row stays
	// readable in the database and reordering the enum cannot silently retype history - the
	// same reasoning as AtsNotification.Type.
	public string VerificationStatus { get; set; } = string.Empty;

	public DateTime? VerifiedAt { get; set; }

	// The breaker. Counts CONSECUTIVE transient failures only, and any success resets it to
	// zero. A permanent recipient-shaped failure (550 no such mailbox) must never land here:
	// that is about the candidate's address, not this account, and counting it would burn every
	// registered account on one batch of bad addresses.
	public int ConsecutiveFailureCount { get; set; }

	// Set when the breaker trips or the provider throttles this account. The selector skips the
	// row until this passes. Null means available.
	public DateTime? CoolingDownUntil { get; set; }

	// The classified SMTP reason behind the last failure, shown in the table so an operator can
	// tell a wrong password from a daily cap without reading logs.
	public string? LastFailureReason { get; set; }

	public DateTime? LastSentAt { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
