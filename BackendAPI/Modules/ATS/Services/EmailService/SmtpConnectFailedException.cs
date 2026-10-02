namespace ATS.Services.EmailService;

/// <summary>
/// Carries an already-classified connect/authenticate failure out of the pool.
///
/// It exists so the send path does not have to re-derive what the pool already worked out.
/// The bug it prevents: a "454 Too many login attempts" raised by AuthenticateAsync used to
/// escape as a bare exception, be read as a generic transient fault, and be retried - which
/// opened another connection and produced another 454.
/// </summary>
public sealed class SmtpConnectFailedException : Exception
{
	public SmtpConnectFailedException(EmailDeliveryResult result, Exception innerException)
		: base(result.Message ?? innerException.Message, innerException)
	{
		Result = result;
	}

	public EmailDeliveryResult Result { get; }
}

/// <summary>
/// Thrown instead of opening a session while the provider is rate limiting authentication.
///
/// Separate from <see cref="SmtpConnectFailedException"/> because nothing was attempted -
/// the pool declined to try. Waiting out a 30-minute login back-off inside the pool would
/// hold a slot for the duration and starve sessions that are still usable.
/// </summary>
public sealed class SmtpLoginThrottledException : Exception
{
	public SmtpLoginThrottledException(string message)
		: base(message)
	{
	}

	public EmailDeliveryResult Result { get; } =
		EmailDeliveryResult.Throttled("454", "Authentication is rate limited by the provider.");
}
