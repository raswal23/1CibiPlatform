namespace ATS.Constants;

/// <summary>
/// The kinds of in-app notification ATS raises. Stored as the string name on
/// <see cref="ATS.Data.Entities.AtsNotification.Type"/>.
/// </summary>
/// <remarks>
/// Strings rather than an enum for the same reason as OrderStatus and OrderHistoryEventType:
/// the value is persisted, so it has to stay readable in the database and survive someone
/// reordering the list. The UI maps each of these to an icon and an accent colour - adding
/// a value here without adding that mapping falls back to the neutral treatment rather
/// than breaking.
/// </remarks>
public static class AtsNotificationType
{
	/// <summary>A candidate completed and submitted the application form we sent them.</summary>
	public const string ApplicationFormSubmitted = "ApplicationFormSubmitted";

	/// <summary>A bulk upload finished parsing; the body carries the accepted/rejected counts.</summary>
	public const string BulkUploadCompleted = "BulkUploadCompleted";

	/// <summary>
	/// Every invitation email for a bulk file has been attempted, e.g. "40/40 sent".
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="BulkUploadCompleted"/>, which fires much earlier: that one
	/// means the file was parsed and the orders exist, this one means the candidates have
	/// actually been contacted. The gap between them can be minutes, and the second is the
	/// one a requestor is waiting on.
	/// </remarks>
	public const string BulkEmailsCompleted = "BulkEmailsCompleted";

	/// <summary>An order reached its terminal Completed state.</summary>
	public const string OrderCompleted = "OrderCompleted";

	/// <summary>A report was uploaded against an order and is ready to read.</summary>
	public const string ReportReady = "ReportReady";

	/// <summary>An order was marked disputed and needs someone to look at it.</summary>
	public const string OrderDisputed = "OrderDisputed";

	/// <summary>OMS ticketing exhausted its automatic retries; the order needs a manual retry.</summary>
	public const string TicketingFailed = "TicketingFailed";

	/// <summary>The invitation email could not be delivered after the configured attempts.</summary>
	public const string InvitationEmailFailed = "InvitationEmailFailed";

	/// <summary>
	/// Every registered sender account is capped, cooling down or disabled, so the invitation
	/// pass stopped with rows still queued.
	/// </summary>
	/// <remarks>
	/// Raised to administrators rather than to the requestor: nothing the requestor can do
	/// fixes it, and the fix - register another sender, or wait out a daily cap - belongs to
	/// whoever manages the accounts. The queued rows are left Pending with their attempt count
	/// unchanged, so this is a pause rather than a failure, but it is silent without this.
	/// </remarks>
	public const string EmailAccountsExhausted = "EmailAccountsExhausted";

	/// <summary>
	/// A sender account's credentials were rejected by the provider, so it was removed from
	/// rotation and needs its password re-entered and re-verified.
	/// </summary>
	/// <remarks>
	/// Separate from <see cref="EmailAccountsExhausted"/> because it is not self-clearing: a
	/// cap lifts on its own after 24 hours, a revoked app password never does. Sending both
	/// under one type would let the actionable one hide behind the transient one.
	/// </remarks>
	public const string EmailAccountNeedsReverification = "EmailAccountNeedsReverification";
}
