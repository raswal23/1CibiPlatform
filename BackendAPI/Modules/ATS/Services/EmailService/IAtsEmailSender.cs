namespace ATS.Services.EmailService;

/// <summary>
/// The result-aware send, kept separate from the shared <c>IEmailService</c>.
///
/// <c>IEmailService</c> lives in BuildingBlocks and is implemented by Auth and the test
/// fakes as well; widening it would force every implementer to reason about SMTP status
/// codes they do not have. Only the ATS bulk path needs to tell "slow down" apart from
/// "no such mailbox", so only ATS declares that contract.
/// </summary>
public interface IAtsEmailSender
{
	/// <summary>
	/// Sends through the highest-priority healthy sender account, moving to the next one when
	/// an account is capped, throttled or rejected.
	/// </summary>
	/// <remarks>
	/// The account is chosen inside the send rather than passed in, so every caller gets
	/// failover without knowing the registry exists. The result reports which account
	/// eventually carried it, because the caller logs and the audit trail need to say more
	/// than "an email went out".
	///
	/// <paramref name="cc"/> is optional and last so the existing single-recipient callers keep
	/// compiling untouched. Copied addresses are real recipients as far as the provider is
	/// concerned, so they are charged to the account's daily cap along with the TO address
	/// rather than riding along free - a message with two copies consumes three, and an
	/// accounting that counted one would show headroom that does not exist.
	/// </remarks>
	Task<EmailDeliveryResult> SendATSEmailWithResultAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null);

	/// <summary>
	/// Sends through ONE named account with no failover, used to prove credentials during
	/// registration and re-verification.
	/// </summary>
	/// <remarks>
	/// Separate from the failover path on purpose. Verification asks "do THESE credentials
	/// work" - silently succeeding through a different account would mark an account verified
	/// on the strength of another one's password, which is the precise failure the OTP exists
	/// to catch.
	/// </remarks>
	Task<EmailDeliveryResult> SendThroughAccountAsync(
		int accountId,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null);

	/// <summary>
	/// Opens a throwaway SMTP session with credentials that are not stored anywhere yet, sends
	/// one message, and closes it.
	/// </summary>
	/// <remarks>
	/// This is what makes registration honest. The credentials are proven BEFORE the row is
	/// trusted, so a mistyped app password surfaces as an immediate, specific error on the
	/// registration form - "authentication failed, check the app password" - rather than as an
	/// invitation queue that has quietly stopped at 3am with nobody watching.
	///
	/// No pool and no shared limiter: this session exists for exactly one message and must not
	/// be reused, because the account it belongs to has not yet earned a place in rotation.
	/// </remarks>
	Task<EmailDeliveryResult> SendWithCredentialsAsync(
		SmtpAccountCredentials credentials,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken);

	/// <summary>
	/// Composes the package follow-up reminder - the second email a candidate gets when the
	/// package's FollowUpEmail interval elapses with the form still unanswered.
	/// </summary>
	/// <remarks>
	/// Here rather than on the shared <c>IEmailService</c> for the same reason as the sends
	/// above: the first-invitation body, <c>SendAppplicationFormNotification</c>, is a
	/// BuildingBlocks contract that Auth and the test fakes implement too, and none of them
	/// have a package follow-up to compose. Only ATS chases, so only ATS declares it.
	///
	/// Composes, it does not send - the caller pairs the returned body with the reminder
	/// subject and hands both to <see cref="SendATSEmailWithResultAsync"/>, so a reminder
	/// travels the same pooled, capped, paced path as every other ATS message.
	///
	/// The link is the candidate's EXISTING one. A reminder that pointed somewhere new would
	/// retire the link in the email they already have, which is exactly what the follow-up is
	/// meant to avoid.
	/// </remarks>
	string BuildApplicationFormReminderNotification(
		string gmail,
		string name,
		string applicationFormLink,
		string? requestor,
		string? clientName);

	/// <summary>
	/// Composes the withdrawal notice - the email the requestor gets when a candidate withdraws
	/// their own application form from the emailed link.
	/// </summary>
	/// <remarks>
	/// On this contract for the same reason as the reminder above: only ATS has a withdrawal to
	/// announce, so the shared <c>IEmailService</c> that Auth and the test fakes implement stays
	/// unaware of it.
	///
	/// Addressed to the REQUESTOR, not the candidate - the candidate is the one who pressed the
	/// button and is copied on the message rather than being told something they just did. The
	/// point of the copy is operational: the verification must not proceed, because the form that
	/// would have authorised it no longer exists.
	///
	/// Composes, it does not send - the caller pairs the body with the withdrawal subject and
	/// hands both to <see cref="SendATSEmailWithResultAsync"/>, so the notice travels the same
	/// pooled, capped, paced path as every other ATS message.
	/// </remarks>
	string BuildWithdrawnApplicationNotification(
		string requestorName,
		string candidateName);

	/// <summary>
	/// Composes the dispute acknowledgement - the email the person who FILED a dispute gets,
	/// confirming it was received and restating what they submitted.
	/// </summary>
	/// <remarks>
	/// Not to be confused with <c>IEmailService.SendEmailForDispute</c>, which composes the
	/// INTERNAL operations notification (a table of requestor email, company, order date and
	/// reason) sent to <c>ATS:DisputeOrderEmailRecipient</c>. Both are sent for one dispute and
	/// they have different audiences, subjects and bodies; this one is the requestor-facing copy.
	///
	/// On this contract for the same reason as the two above: only ATS files disputes, so the
	/// shared <c>IEmailService</c> that Auth and the test fakes implement stays unaware of it.
	///
	/// <paramref name="disputeDetails"/> is nullable because the console only captures free text
	/// for the "Others" category - a Billing or Report dispute has a category and nothing else.
	/// The details bullet is omitted rather than rendered empty or padded with a placeholder.
	///
	/// Composes, it does not send - the caller pairs the body with <c>DisputeEmail.Subject</c> and
	/// hands both to <see cref="SendATSEmailWithResultAsync"/>.
	/// </remarks>
	string BuildDisputeNotification(
		string requestorName,
		string candidateName,
		string disputeCategory,
		string? disputeDetails);
}
