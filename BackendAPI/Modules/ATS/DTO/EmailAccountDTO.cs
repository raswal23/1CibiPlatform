namespace ATS.DTO;

/// <summary>
/// One sender account as the management table sees it.
/// </summary>
/// <remarks>
/// Projected from <c>AtsEmailAccountSnapshot</c>, which has no password field at all - so "the
/// API cannot leak the app password" holds because there is nothing to leak, not because every
/// future endpoint remembers to strip it. <see cref="HasPassword"/> reports only that one is
/// stored, which is all the UI needs to say "leave blank to keep the current password".
/// </remarks>
public class EmailAccountDTO
{
	public int AtsEmailAccountId { get; set; }

	public string DisplayName { get; set; } = string.Empty;

	public string EmailAddress { get; set; } = string.Empty;

	public string SmtpHost { get; set; } = string.Empty;

	public int SmtpPort { get; set; }

	public int Priority { get; set; }

	public bool IsActive { get; set; }

	public int DailySendLimit { get; set; }

	public string VerificationStatus { get; set; } = string.Empty;

	public DateTime? VerifiedAt { get; set; }

	public int ConsecutiveFailureCount { get; set; }

	public DateTime? CoolingDownUntil { get; set; }

	public string? LastFailureReason { get; set; }

	public DateTime? LastSentAt { get; set; }

	/// <summary>Recipients sent in the rolling window - the numerator of the Consumed column.</summary>
	public int ConsumedInWindow { get; set; }

	public int RemainingInWindow { get; set; }

	/// <summary>True while a send is in flight through this account.</summary>
	/// <remarks>
	/// Carried so the table can show an "In use" badge that explains why edit and delete are
	/// refused, rather than presenting the ConflictException as an unexplained error.
	/// </remarks>
	public bool IsInUse { get; set; }

	/// <summary>
	/// Whether this account may carry the next message, as the selector would decide it.
	/// </summary>
	/// <remarks>
	/// Computed on the server from the same snapshot the selector reads, rather than re-derived
	/// in the browser. A UI that computed "sendable" itself would drift from the routing rule the
	/// moment either side changed, and the table would confidently show a green pill for an
	/// account nothing is being sent through.
	/// </remarks>
	public bool IsSendable { get; set; }

	public bool HasPassword { get; set; }
}
