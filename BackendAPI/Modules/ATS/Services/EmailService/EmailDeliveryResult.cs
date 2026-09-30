namespace ATS.Services.EmailService;

/// <summary>
/// What the SMTP server actually said, rather than the bool the send used to return.
///
/// The distinction is the whole point: a mistyped address and a "you are going too fast"
/// are both failures, but retrying the first wastes the row's budget and retrying the
/// second is what extends a ten-minute throttle into an hour.
/// </summary>
public enum EmailDeliveryOutcome
{
	/// <summary>The server accepted the message. It owns delivery from here.</summary>
	Sent,

	/// <summary>
	/// A temporary fault (4xx, socket drop, timeout). Worth retrying after a back-off.
	/// </summary>
	Transient,

	/// <summary>
	/// The server refused permanently (5xx): unknown mailbox, rejected sender, malformed
	/// address. Retrying produces the identical refusal, so the row fails immediately
	/// instead of consuming five attempts.
	/// </summary>
	Permanent,

	/// <summary>
	/// The provider is rate limiting this sender (421, 454, "try again later"). Every
	/// remaining send THROUGH THIS ACCOUNT must stop - not slow down, stop. Continuing to
	/// knock is what turns a short deferral into a long block. The pass itself continues on
	/// the next registered account.
	/// </summary>
	Throttled
}

/// <summary>
/// Whether a failure was about the MESSAGE or about the ACCOUNT that carried it.
/// </summary>
/// <remarks>
/// This is the distinction the switcher turns on, and getting it backwards is the most
/// expensive mistake available here.
///
/// A "550 no such mailbox" is about the candidate's address. Counting it against the sender
/// would retire a perfectly healthy Gmail, and one bulk upload full of typo'd addresses would
/// burn every registered account in minutes - leaving the queue with nowhere to send and
/// nothing actually wrong.
///
/// A "535 bad credentials" or a "454 too many login attempts" carries no information about the
/// recipient at all. Retrying it on the same account produces the identical refusal, so the
/// only useful response is to move.
/// </remarks>
public enum EmailFailureScope
{
	/// <summary>
	/// About this message or this recipient. The account is fine; the next message goes down
	/// the same connection.
	/// </summary>
	Message,

	/// <summary>
	/// About the sending account. This message is worth retrying, but only somewhere else.
	/// </summary>
	Account
}

public sealed record EmailDeliveryResult(
	EmailDeliveryOutcome Outcome,
	string? StatusCode = null,
	string? Message = null,
	EmailFailureScope Scope = EmailFailureScope.Message)
{
	public bool IsSent => Outcome == EmailDeliveryOutcome.Sent;

	/// <summary>
	/// True when this failure means "stop using this account", rather than "this recipient
	/// was refused".
	/// </summary>
	/// <remarks>
	/// Carried on the result rather than re-derived by the caller, because by the time a
	/// result reaches the processor the evidence is gone: MailKit's AuthenticationException
	/// has no status code, so <see cref="SmtpFailureClassifier.ClassifyConnectFailure"/>
	/// returns a Permanent with a NULL <see cref="StatusCode"/>. Anything downstream trying to
	/// recognise "535" by sniffing the code would silently never match, and a wrong password
	/// would look exactly like a bad recipient address.
	/// </remarks>
	public bool IsAccountFault =>
		!IsSent && Scope == EmailFailureScope.Account;

	/// <summary>
	/// True when THIS message may safely be re-sent through a different account right now.
	/// </summary>
	/// <remarks>
	/// Narrower than <see cref="IsAccountFault"/>, and deliberately so. The breaker counts every
	/// account fault, but only some of them are safe to resend immediately.
	///
	/// A throttle or an auth rejection is refused before the message body is ever accepted, so
	/// nothing was delivered and sending it elsewhere delivers it once. A transient socket drop
	/// or timeout is the dangerous case: it can fire AFTER the provider accepted the message -
	/// that is how a candidate received the same invitation twice - so resending it on another
	/// account in the same breath would turn a rare duplicate into a reliable one. A transient
	/// still counts toward the breaker and can still take the account out of rotation for
	/// SUBSEQUENT messages; this one is left to the normal deferral path.
	/// </remarks>
	public bool CanRetryOnAnotherAccount =>
		Scope == EmailFailureScope.Account
		&& Outcome is EmailDeliveryOutcome.Throttled or EmailDeliveryOutcome.Permanent;

	public static readonly EmailDeliveryResult Sent = new(EmailDeliveryOutcome.Sent);

	public static EmailDeliveryResult Transient(
		string? code,
		string? message,
		EmailFailureScope scope = EmailFailureScope.Message) =>
		new(EmailDeliveryOutcome.Transient, code, message, scope);

	public static EmailDeliveryResult Permanent(
		string? code,
		string? message,
		EmailFailureScope scope = EmailFailureScope.Message) =>
		new(EmailDeliveryOutcome.Permanent, code, message, scope);

	// Always the account's fault by definition: a throttle is the provider talking about this
	// sender's rate, never about who the message was addressed to.
	public static EmailDeliveryResult Throttled(string? code, string? message) =>
		new(EmailDeliveryOutcome.Throttled, code, message, EmailFailureScope.Account);
}
