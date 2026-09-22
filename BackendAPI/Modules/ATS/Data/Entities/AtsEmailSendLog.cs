namespace ATS.Data.Entities;

/// <summary>
/// One row per message successfully handed to a sender account, used to compute that
/// account's rolling-24h consumption.
/// </summary>
/// <remarks>
/// A counter column on <see cref="AtsEmailAccount"/> would be smaller, but it cannot express a
/// rolling window: it would need resetting at some fixed hour, and Gmail does not enforce its
/// limit at a fixed hour - a send at 23:00 still counts against you at 22:00 the next day. A
/// row per send makes the figure a COUNT over a WHERE, which is the same number the selector
/// and the UI read, so the table can never disagree with the routing decision.
///
/// Only SUCCESSFUL sends are logged. A message the provider refused did not consume quota, and
/// logging it would make the account look more consumed than it is and retire it early.
///
/// Rows are swept after 48 hours by AtsEmailSendLogRetentionService: nothing reads past 24, and
/// the extra day is only there so a clock skew or a paused sweep cannot erase a live window.
/// </remarks>
public sealed class AtsEmailSendLog
{
	public long AtsEmailSendLogId { get; set; }

	public int AtsEmailAccountId { get; set; }

	// The TO address only - always 1 as written today, so this SUM and a COUNT(*) agree.
	// Copied addresses are real recipients to the provider and DO consume its cap, but they are
	// deliberately not charged here (see ATSEmailService.SendThroughAccountAsync), so this
	// figure runs below true provider consumption whenever a message carries a copy list.
	// Kept as a column rather than dropped because the shortfall is the thing most likely to
	// need correcting later, and a per-row number is where that correction would go.
	public int RecipientCount { get; set; }

	public DateTime SentAt { get; set; }
}
