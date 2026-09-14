namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// A registered sender mailbox as the table shows it, mirroring <c>ATS.DTO.EmailAccountDTO</c>.
/// </summary>
/// <remarks>
/// Carries no password in any form - not the plaintext, not the protected value, not a length.
/// <see cref="HasPassword"/> is the only thing the edit dialog needs: it tells the operator a
/// credential is already stored so leaving the field blank keeps it.
/// </remarks>
public class EmailAccountDTO
{
	public int AtsEmailAccountId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string EmailAddress { get; set; } = string.Empty;
	public string SmtpHost { get; set; } = string.Empty;
	public int SmtpPort { get; set; }

	/// <summary>Lower is preferred. The selector walks these in ascending order.</summary>
	public int Priority { get; set; }

	public bool IsActive { get; set; }
	public int DailySendLimit { get; set; }

	/// <summary>Pending, Verified or NeedsReverification.</summary>
	public string VerificationStatus { get; set; } = string.Empty;

	public DateTime? VerifiedAt { get; set; }

	/// <summary>Transient failures in a row. Resets to zero on any success.</summary>
	public int ConsecutiveFailureCount { get; set; }

	public DateTime? CoolingDownUntil { get; set; }
	public string? LastFailureReason { get; set; }
	public DateTime? LastSentAt { get; set; }

	/// <summary>Recipients sent in the trailing 24 hours - what Gmail actually counts.</summary>
	public int ConsumedInWindow { get; set; }

	public int RemainingInWindow { get; set; }

	/// <summary>True while a send is in flight through this account, which blocks edit and delete.</summary>
	public bool IsInUse { get; set; }

	/// <summary>True when the selector would hand this account the next message.</summary>
	public bool IsSendable { get; set; }

	public bool HasPassword { get; set; }
}
