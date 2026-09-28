namespace ATS.Constants;

/// <summary>
/// The subject of the notice sent to the requestor when a dispute is raised on an order.
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
///
/// The copy list used to live here too, as <c>CopyTeam</c>. It is now the
/// <see cref="AtsEmailProcess.Dispute"/> row in <c>ats."EmailProcessDetails"</c>, read by
/// <c>IEmailProcessManagementService.GetCopyListAsync</c>. Note what that moves: the body's closing
/// sentence still names <c>clientsupport@cibi.com.ph</c> and <c>ccteam@cibi.com.ph</c> as PROSE,
/// and nothing keeps the sentence in step with the row. An operator retiring an address from the
/// table will leave the message telling its reader to write to a mailbox nobody is reading - see
/// the wiring table in <c>docs/features/ats-dispute-order-email/</c>.
/// </remarks>
public static class DisputeEmail
{
	/// <summary>
	/// The subject line, and the header inside the composed body.
	/// </summary>
	public const string Subject = "CIBI | Order Dispute";
}
