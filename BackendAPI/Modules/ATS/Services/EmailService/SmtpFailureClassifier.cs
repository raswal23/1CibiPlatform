namespace ATS.Services.EmailService;

/// <summary>
/// Reads an SMTP failure the same way wherever it happened - during CONNECT, during
/// AUTHENTICATE, or during SEND.
///
/// This is shared rather than living in the send path because the first version of this
/// code only classified send failures. "454 Too many login attempts" is raised by
/// AuthenticateAsync, so it escaped the send path's try/catch entirely, was reported as an
/// unclassified transient fault, and got retried - which opened another connection and
/// produced another 454. The classifier has to be reachable from the place the connection
/// is built, or the login throttle is invisible to the code that must react to it.
/// </summary>
public static class SmtpFailureClassifier
{
	/// <summary>
	/// True when the message says the provider is deliberately slowing this sender down,
	/// rather than reporting something about the recipient.
	/// </summary>
	private static bool LooksLikeThrottle(string? message) =>
		message is not null
		&& (message.Contains("try again later", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("unusual rate", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("too many", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Classifies a failure raised while CONNECTING or AUTHENTICATING.
	///
	/// Anything throttle-shaped here is a LOGIN throttle, which is a different budget from
	/// the send rate and needs the longer back-off.
	/// </summary>
	public static EmailDeliveryResult ClassifyConnectFailure(Exception exception)
	{
		if (exception is MailKit.Security.AuthenticationException authException)
		{
			// MailKit wraps the server's refusal. A throttle-shaped message is the provider
			// rate limiting logins; anything else is a genuinely bad credential, which no
			// amount of retrying will fix.
			//
			// Scoped to the ACCOUNT either way. Note the status code is null here - MailKit's
			// AuthenticationException does not carry one - which is exactly why the scope has
			// to be decided at this point: nothing downstream can recognise a 535 by looking.
			return LooksLikeThrottle(authException.Message)
				? EmailDeliveryResult.Throttled("454", authException.Message)
				: EmailDeliveryResult.Permanent(
					null,
					authException.Message,
					EmailFailureScope.Account);
		}

		if (exception is MailKit.Net.Smtp.SmtpCommandException commandException)
		{
			var code = (int)commandException.StatusCode;

			if (code is 421 or 454 || LooksLikeThrottle(commandException.Message))
			{
				return EmailDeliveryResult.Throttled(
					code.ToString(CultureInfo.InvariantCulture),
					commandException.Message);
			}

			// Everything raised during CONNECT or AUTHENTICATE belongs to the account: no
			// recipient has been named yet, so the server cannot be complaining about one.
			return code >= 500
				? EmailDeliveryResult.Permanent(
					code.ToString(CultureInfo.InvariantCulture),
					commandException.Message,
					EmailFailureScope.Account)
				: EmailDeliveryResult.Transient(
					code.ToString(CultureInfo.InvariantCulture),
					commandException.Message,
					EmailFailureScope.Account);
		}

		// A socket that will not open, a TLS negotiation that failed, a DNS miss. Transient:
		// the provider may simply be unreachable right now.
		//
		// Scoped to the account even though the cause may be the network. Moving to the next
		// account costs one retry; staying on a host we cannot reach costs the whole pass. If
		// the network is down, the next account fails the same way and the pass ends anyway.
		return EmailDeliveryResult.Transient(null, exception.Message, EmailFailureScope.Account);
	}

	/// <summary>
	/// Classifies a failure raised while SENDING, and says whether the session survives.
	///
	/// The second half matters as much as the first. Discarding a session forces the next
	/// caller to log in again, so treating every failure as fatal to the connection turns
	/// one throttle into a stream of logins - the exact loop that produced "454 Too many
	/// login attempts" here.
	/// </summary>
	public static (EmailDeliveryResult Result, bool SessionIsUsable) ClassifySendFailure(
		MailKit.Net.Smtp.SmtpCommandException exception)
	{
		var code = (int)exception.StatusCode;
		var codeText = code.ToString(CultureInfo.InvariantCulture);

		if (code is 421 or 454 || LooksLikeThrottle(exception.Message))
		{
			// 421 is "service closing transmission channel" - the server has hung up, so the
			// session is genuinely gone. Every other throttle leaves the connection open,
			// and keeping it is what avoids a re-login.
			var serverClosedConnection = code == 421;

			return (
				EmailDeliveryResult.Throttled(codeText, exception.Message),
				!serverClosedConnection);
		}

		// A per-recipient rejection says nothing about the connection: the session is still
		// good and the next message can go down it.
		//
		// Nor does it say anything about the ACCOUNT - with one exception. Most 5xx codes here
		// are about the address we just gave the server, but a few are the server refusing the
		// SENDER, and those two must not be conflated: counting a bad recipient against the
		// account would let one bulk upload of typo'd addresses retire every registered
		// mailbox, while ignoring a rejected sender would keep re-offering messages to an
		// account the provider has already disowned.
		var scope = code >= 500 && LooksLikeSenderRejection(exception.Message)
			? EmailFailureScope.Account
			: EmailFailureScope.Message;

		return code >= 500
			? (EmailDeliveryResult.Permanent(codeText, exception.Message, scope), true)
			: (EmailDeliveryResult.Transient(codeText, exception.Message, scope), true);
	}

	/// <summary>
	/// True when a 5xx refusal names the SENDER rather than the recipient.
	/// </summary>
	/// <remarks>
	/// Deliberately narrow, and it should stay that way. Every phrase here is one a provider
	/// uses to say "this mailbox may not send", not "that mailbox does not exist":
	///
	/// - "5.4.5 Daily user sending limit exceeded" - Gmail's daily cap, which is precisely the
	///   wall this whole feature exists to route around.
	/// - "sender address rejected" / "not allowed to send" - the account is not permitted to
	///   send as this From address.
	/// - "account has been disabled" / "suspended" - the mailbox itself is gone.
	///
	/// Widening this to anything resembling "5xx looks serious" would put recipient failures
	/// back in the account bucket and reintroduce the exact bug the split exists to prevent.
	/// </remarks>
	private static bool LooksLikeSenderRejection(string? message) =>
		message is not null
		&& (message.Contains("daily user sending limit", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("daily sending quota", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("sending limit exceeded", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("sender address rejected", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("not allowed to send", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("account has been disabled", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("account is disabled", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("account suspended", StringComparison.OrdinalIgnoreCase));
}
