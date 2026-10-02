namespace ATS.Constants;

/// <summary>
/// The subject of the notice sent when a candidate withdraws their own application form.
/// </summary>
/// <remarks>
/// Here rather than private to <c>ApplicationFormService</c> because the subject is not only that
/// service's business: <c>ATSEmailService.BuildWithdrawnApplicationNotification</c> renders it as
/// the body's header, and a recipient reads the two together in a mail client's preview. A header
/// that disagreed with the subject line would look like a mis-send, so both sites read this one
/// constant instead of one of them keeping a copy that nothing forces it to update.
///
/// The copy list used to live here too, as <c>CopyTeam</c>. It is now the
/// <see cref="AtsEmailProcess.Withdrawn"/> row in <c>ats."EmailProcessDetails"</c>, read by
/// <c>IEmailProcessManagementService.GetCopyListAsync</c> - a mailbox can be added or retired
/// without a rebuild. A subject is a different kind of value: changing one rewords the message,
/// which is a copy decision made with the body beside it, so it stays compiled in.
/// </remarks>
public static class WithdrawnEmail
{
	/// <summary>
	/// The subject line, and the header inside the composed body.
	/// </summary>
	public const string Subject = "Order Status – Withdrawn";
}
