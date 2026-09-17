namespace ATS.Constants;

/// <summary>
/// The fixed parts of the notice sent when a candidate withdraws their own application form.
/// </summary>
/// <remarks>
/// Here rather than private to <c>ApplicationFormService</c> because the subject is not only that
/// service's business: <c>ATSEmailService.BuildWithdrawnApplicationNotification</c> renders it as
/// the body's header, and a recipient reads the two together in a mail client's preview. A header
/// that disagreed with the subject line would look like a mis-send, so both sites read this one
/// constant instead of one of them keeping a copy that nothing forces it to update.
/// </remarks>
public static class WithdrawnEmail
{
	/// <summary>
	/// The subject line, and the header inside the composed body.
	/// </summary>
	public const string Subject = "Order Status – Withdrawn";

	/// <summary>
	/// Copied on every withdrawal notice, so CIBI sees each one regardless of which requestor
	/// raised the order.
	/// </summary>
	/// <remarks>
	/// The candidate joins the copy at send time rather than being listed here: whether a given
	/// order has a candidate address on it is the caller's knowledge, and a constant that could be
	/// empty would put an unparseable mailbox on the message.
	/// </remarks>
	public const string CopyTeam = "ccteam@cibi.com.ph";
}
