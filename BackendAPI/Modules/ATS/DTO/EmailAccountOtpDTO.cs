namespace ATS.DTO;

/// <summary>
/// What the caller is told after a code has been sent.
/// </summary>
/// <remarks>
/// Carries no code and no credential - only where it went and when it lapses, so the dialog can
/// say "we sent a code to ops@cibi.com, it expires in 10 minutes" without the server having told
/// the browser anything it could replay.
/// </remarks>
public class EmailAccountOtpSentDTO
{
	public int AtsEmailAccountId { get; set; }

	/// <summary>AtsEmailAccountOtpPurpose - which dialog should open next.</summary>
	public string Purpose { get; set; } = string.Empty;

	/// <summary>The mailbox the code went to, echoed so the dialog can name it.</summary>
	public string EmailAddress { get; set; } = string.Empty;

	public DateTime ExpiresAt { get; set; }

	public int ExpiryInMinutes { get; set; }
}

/// <summary>
/// The result of submitting a code.
/// </summary>
/// <remarks>
/// <see cref="RemainingAttempts"/> is returned on failure so the operator can see the cap
/// closing rather than meeting a sudden "code no longer valid" on the fifth try.
/// </remarks>
public class EmailAccountOtpResultDTO
{
	public bool IsVerified { get; set; }

	public int RemainingAttempts { get; set; }

	public string? Message { get; set; }
}

/// <summary>A code submitted for one account and purpose.</summary>
public class VerifyEmailAccountOtpDTO
{
	public int AtsEmailAccountId { get; set; }

	public string Purpose { get; set; } = string.Empty;

	public string OtpCode { get; set; } = string.Empty;
}

/// <summary>A request for a fresh code.</summary>
public class ResendEmailAccountOtpDTO
{
	public int AtsEmailAccountId { get; set; }

	public string Purpose { get; set; } = string.Empty;
}

/// <summary>
/// A delete request, which mints a code rather than removing anything.
/// </summary>
/// <remarks>
/// Deletion is a two-step for the same reason registration is: the remaining accounts absorb the
/// deleted one's volume, and if it was the last one the queue stops. The code itself comes back
/// through <see cref="VerifyEmailAccountOtpDTO"/> with a Delete purpose, so all three flows
/// confirm through one endpoint and there is only one place attempts are counted.
/// </remarks>
public class DeleteEmailAccountDTO
{
	public int AtsEmailAccountId { get; set; }
}
