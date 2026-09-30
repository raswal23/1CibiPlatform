namespace ATS.Constants;

/// <summary>
/// The limits on a sender-account verification code.
/// </summary>
/// <remarks>
/// Mirrors Auth's RegisterService rather than inventing new numbers: an operator who has verified
/// an account here and registered a user there should not meet two different rules for the same
/// six digits.
/// </remarks>
public static class AtsEmailAccountOtpPolicy
{
	/// <summary>
	/// Wrong guesses allowed before the code is consumed and a new one must be requested.
	/// </summary>
	/// <remarks>
	/// Without a cap a six-digit code is a million cheap guesses. Consuming the code rather than
	/// locking the account is deliberate - the account is not yet trusted, so there is nothing to
	/// lock, and a resend costs one email.
	/// </remarks>
	public const int MaxAttempts = 5;

	/// <summary>Fallback when ATS:EmailAccountOtpExpiryInMinutes is absent.</summary>
	public const int DefaultExpiryInMinutes = 10;

	public const string ConfigurationKey = "ATS:EmailAccountOtpExpiryInMinutes";
}
