namespace ATS.Services.EmailAccounts;

/// <summary>
/// One sender account as the selector and the management table see it: identity, health and
/// consumption, with no credential of any kind.
/// </summary>
/// <remarks>
/// Separate from the entity so that nothing outside the registry ever holds an object with a
/// password on it. The read DTO is projected from this, which makes "the API cannot leak the
/// password" a property of the type rather than a rule someone has to remember.
/// </remarks>
public sealed record AtsEmailAccountSnapshot(
	int AtsEmailAccountId,
	string DisplayName,
	string EmailAddress,
	string SmtpHost,
	int SmtpPort,
	int Priority,
	bool IsActive,
	int DailySendLimit,
	string VerificationStatus,
	DateTime? VerifiedAt,
	int ConsecutiveFailureCount,
	DateTime? CoolingDownUntil,
	string? LastFailureReason,
	DateTime? LastSentAt,

	// Recipients sent in the rolling window, counted from the send log rather than a column.
	int ConsumedInWindow)
{
	public bool IsVerified =>
		VerificationStatus == AtsEmailAccountStatus.Verified;

	public bool IsCoolingDown(DateTime asOfUtc) =>
		CoolingDownUntil is { } until && asOfUtc < until;

	public int RemainingInWindow =>
		Math.Max(0, DailySendLimit - ConsumedInWindow);

	/// <summary>
	/// Whether this account may carry the next message.
	/// </summary>
	/// <remarks>
	/// The quota check is a strict "is there room left", evaluated BEFORE the send. That
	/// ordering is the entire value of tracking consumption: reacting to the provider's
	/// refusal is too late, because a Gmail that has answered "5.4.5 Daily user sending limit
	/// exceeded" is locked for roughly 24 hours. Moving one message early costs nothing.
	/// </remarks>
	public bool IsSendable(DateTime asOfUtc) =>
		IsActive
		&& IsVerified
		&& !IsCoolingDown(asOfUtc)
		&& RemainingInWindow > 0;
}
