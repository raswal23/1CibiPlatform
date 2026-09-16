namespace ATS.Constants;

/// <summary>
/// The fixed parts of the notice sent when a candidate completes their application form.
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
/// </remarks>
public static class SubmittedFormEmail
{
	/// <summary>
	/// The subject line, and the header inside the composed body.
	/// </summary>
	public const string Subject = "CIBI | Order Status – In Progress";

	/// <summary>
	/// The CIBI mailboxes copied on every completed-form notice, so both teams see each one
	/// regardless of which requestor raised the order. The candidate is added to this list at send
	/// time, from the address the invitation link was sent to.
	/// </summary>
	/// <remarks>
	/// The body's closing sentence names <c>ccteam@cibi.com.ph</c> and
	/// <c>clientsupport@cibi.com.ph</c> as prose, but only the former is actually copied here -
	/// <c>pre-workteam</c> is on the message and not in the sentence, and <c>clientsupport</c> is in
	/// the sentence and not on the message. That mismatch is in the agreed copy and is reproduced
	/// deliberately; see the wiring table in
	/// <c>docs/features/ats-submitted-form-email/</c> before changing either side.
	/// </remarks>
	public static readonly IReadOnlyCollection<string> CopyTeams =
	[
		"ccteam@cibi.com.ph",
		"pre-workteam@cibi.com.ph"
	];
}
