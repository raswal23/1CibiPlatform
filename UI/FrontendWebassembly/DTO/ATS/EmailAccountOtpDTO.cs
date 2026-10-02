namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// The purposes a sender-account code can be issued for. Mirrors
/// <c>ATS.Constants.AtsEmailAccountOtpPurpose</c>; the server rejects anything else.
/// </summary>
public static class EmailAccountOtpPurposes
{
	public const string Register = "Register";
	public const string Edit = "Edit";
	public const string Delete = "Delete";
}

/// <summary>Where a code went and when it lapses. Carries no code.</summary>
public class EmailAccountOtpSentDTO
{
	public int AtsEmailAccountId { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public string EmailAddress { get; set; } = string.Empty;
	public DateTime ExpiresAt { get; set; }
	public int ExpiryInMinutes { get; set; }
}

/// <summary>
/// The result of submitting a code.
/// </summary>
/// <remarks>
/// A wrong code comes back as a 200 with <see cref="IsVerified"/> false, not an HTTP error, so
/// the dialog can show <see cref="RemainingAttempts"/> rather than a bare failure.
/// </remarks>
public class EmailAccountOtpResultDTO
{
	public bool IsVerified { get; set; }
	public int RemainingAttempts { get; set; }
	public string? Message { get; set; }
}

public class VerifyEmailAccountOtpDTO
{
	public int AtsEmailAccountId { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public string OtpCode { get; set; } = string.Empty;
}

public class ResendEmailAccountOtpDTO
{
	public int AtsEmailAccountId { get; set; }
	public string Purpose { get; set; } = string.Empty;
}

/// <summary>Asks for a deletion code. Removes nothing on its own.</summary>
public class DeleteEmailAccountDTO
{
	public int AtsEmailAccountId { get; set; }
}
