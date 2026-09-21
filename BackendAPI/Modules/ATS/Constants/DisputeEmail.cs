namespace ATS.Constants;

/// <summary>
/// The fixed parts of the notice sent to the requestor when a dispute is raised on an order.
/// </summary>
/// <remarks>
/// The only email a dispute produces. An earlier internal operations alert used its own subject
/// literal in <c>DisputeOrderService</c> and went to the <c>ATS:DisputeOrderEmailRecipient</c>
/// mailbox; both were removed, so this is the one message and this is where its subject lives.
///
/// The subject lives here rather than beside the send because
/// <c>ATSEmailService.BuildDisputeNotification</c> renders it as the body's header, and a recipient
/// reads the two together in a mail client's preview - a header that disagreed with the subject
/// would look like a mis-send.
/// </remarks>
public static class DisputeEmail
{
	/// <summary>
	/// The subject line, and the header inside the composed body.
	/// </summary>
	public const string Subject = "CIBI | Order Dispute";

	/// <summary>
	/// Copied on every dispute notice so CIBI client support sees each one regardless of which
	/// requestor filed it.
	/// </summary>
	/// <remarks>
	/// The body's closing sentence names this address AND <c>ccteam@cibi.com.ph</c> as prose, but
	/// only this one is actually copied. Changing the constant does not change the sentence - see
	/// the wiring table in <c>docs/features/ats-dispute-order-email/</c>.
	/// </remarks>
	//public const string CopyTeam = "clientsupport@cibi.com.ph";
	public const string CopyTeam = "svaldemoro@cibi.com.ph";
}
