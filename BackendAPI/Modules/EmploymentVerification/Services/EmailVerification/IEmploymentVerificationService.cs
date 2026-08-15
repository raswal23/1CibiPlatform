namespace EmploymentVerification.Services;

public sealed record CreateEmploymentVerificationRequest(
	string CandidateName,
	string PreviousEmployer,
	string Position,
	string HrEmail,
	DateTime? EmploymentStartDate,
	DateTime? EmploymentEndDate,
	Guid? AtsSubjectId);

public interface IEmploymentVerificationService
{
	Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Lists every request raised from this module for the tracking view, without
	/// exposing the verification token hash that secures the emailed link.
	/// </summary>
	Task<IReadOnlyList<SentVerificationRequestDTO>> ListSentRequestsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Lists in-progress ATS candidates that still need a verification email.
	/// Candidates with a request awaiting a response, or already verified, are
	/// withheld until that request is rejected or its link lapses.
	/// </summary>
	Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetAvailableATSRecordsAsync(CancellationToken cancellationToken);
	Task<EmploymentVerificationRequest> CreateAndSendAsync(CreateEmploymentVerificationRequest request, CancellationToken cancellationToken);
	/// <summary>
	/// Records the HR contact's response against the emailed token. Set
	/// <paramref name="reject"/> to mark the details inaccurate instead of confirmed.
	/// </summary>
	Task<EmploymentVerificationCompletionResult> VerifyAsync(string token, bool reject, CancellationToken cancellationToken);

	/// <summary>
	/// Validates the emailed token and, when it is still actionable, returns the
	/// request details for the anonymous confirmation page.
	/// </summary>
	Task<EmploymentVerificationPreviewResult> GetPreviewByTokenAsync(string token, CancellationToken cancellationToken);
}
