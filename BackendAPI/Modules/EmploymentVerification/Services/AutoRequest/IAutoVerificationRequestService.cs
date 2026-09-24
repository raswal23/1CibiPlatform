namespace EmploymentVerification.Services.AutoRequest;

/// <summary>
/// Where the address a verification request was sent to came from.
/// </summary>
public enum VerificationRecipientSource
{
	/// <summary>
	/// An address listed in the contact directory. The only source the automatic sender
	/// will write to - the supervisor address from the form must appear there first.
	/// </summary>
	Directory,

	/// <summary>
	/// An address supplied outside the directory. Never produced by the automatic
	/// sender; retained because rows created before the directory became the gate
	/// carry it, and the tracking view still renders them.
	/// </summary>
	CandidateSupplied
}

/// <summary>What one pass of the automatic sender did, for the job to log.</summary>
public sealed record AutoVerificationRequestResult(
	int Eligible,
	int Sent,
	int SkippedNoConsent,
	int SkippedNoRecipient,
	int Failed,
	bool Truncated);

public interface IAutoVerificationRequestService
{
	/// <summary>
	/// Sends a verification request for every employment slot that is eligible and has
	/// not been contacted yet.
	/// </summary>
	/// <remarks>
	/// Per slot, both must hold: the candidate granted permission to contact that
	/// employer, and the supervisor address they supplied is listed in the contact
	/// directory. An address the directory does not know is left for an operator
	/// rather than written to.
	/// </remarks>
	Task<AutoVerificationRequestResult> SendDueRequestsAsync(CancellationToken cancellationToken);
}
