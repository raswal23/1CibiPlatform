namespace ATS.Constants;

/// <summary>
/// The subject of the notice sent when a candidate completes their application form.
/// </summary>
/// <remarks>
/// Here rather than private to the notifier for the same reason as <see cref="WithdrawnEmail"/> and
/// <see cref="DisputeEmail"/>: <c>ATSEmailService.BuildSubmittedFormNotification</c> renders the
/// subject as the body's header, and a recipient reads the two together in a mail client's preview.
/// A header that disagreed with the subject line would look like a mis-send, so both sites read this
/// one constant.
///
/// The subject says "In Progress" rather than "Submitted" because that is the order status the
/// submission moves the row to (<c>OrderStatus.InProgress</c>, set by
/// <c>UpdateEmailInvitationRequestForFilledUpFormAsync</c>). Naming the state the requestor will see
/// in the console keeps the email and the grid in agreement.
///
/// The copy list used to live here too, as <c>CopyTeams</c>. It is now the
/// <see cref="AtsEmailProcess.SubmittedForm"/> row in <c>ats."EmailProcessDetails"</c>, read by
/// <c>IEmailProcessManagementService.GetCopyListAsync</c>. The mismatch that list carried is worth
/// restating because moving it to a row does not resolve it: the body's closing sentence names
/// <c>ccteam@cibi.com.ph</c> and <c>clientsupport@cibi.com.ph</c> as PROSE, while the seeded row
/// copies <c>clientsupport</c> and <c>pre-workteam</c>. So <c>pre-workteam</c> is on the message
/// and not in the sentence, and <c>ccteam</c> is in the sentence and not on the message. That is
/// the agreed copy, reproduced deliberately - and now an operator can widen the gap without
/// touching the body. See the wiring
/// table in <c>docs/features/ats-submitted-form-email/</c> before changing either side.
/// </remarks>
public static class SubmittedFormEmail
{
	/// <summary>
	/// The subject line, and the header inside the composed body.
	/// </summary>
	public const string Subject = "CIBI | Order Status – In Progress";
}
