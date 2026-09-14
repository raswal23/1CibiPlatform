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
	/// </remarks>
	Task<EmailDeliveryResult> SendATSEmailWithResultAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken);

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
		CancellationToken cancellationToken);

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
}
