namespace FrontendWebassembly.ShareData.ATS;

/// <summary>
/// Notification type names, mirroring <c>ATS.Constants.AtsNotificationType</c> on the
/// backend. The value arrives as a string on the DTO; these are what the UI matches on to
/// pick an icon and an accent.
/// </summary>
/// <remarks>
/// Adding a type on the backend without adding it here is not a break: the item component
/// falls back to a neutral bell rather than failing to render. Keep them in step anyway,
/// or the new type stays visually undifferentiated.
/// </remarks>
public static class AtsNotificationTypes
{
	public const string ApplicationFormSubmitted = "ApplicationFormSubmitted";
	public const string BulkUploadCompleted = "BulkUploadCompleted";

	// Fires later than BulkUploadCompleted: that one means the file was parsed, this one
	// means every candidate has actually been emailed.
	public const string BulkEmailsCompleted = "BulkEmailsCompleted";
	public const string OrderCompleted = "OrderCompleted";
	public const string ReportReady = "ReportReady";
	public const string OrderDisputed = "OrderDisputed";
	public const string TicketingFailed = "TicketingFailed";
	public const string InvitationEmailFailed = "InvitationEmailFailed";

	// Every sender account is capped, cooling down or disabled, so the invitation pass
	// stopped with rows still queued. The rows keep their attempt count, so this is a
	// pause rather than a failure - and it is silent without the notification.
	public const string EmailAccountsExhausted = "EmailAccountsExhausted";

	// Separate from EmailAccountsExhausted because it does not clear itself: a daily cap
	// lifts after 24 hours, a revoked app password never does.
	public const string EmailAccountNeedsReverification = "EmailAccountNeedsReverification";
}
