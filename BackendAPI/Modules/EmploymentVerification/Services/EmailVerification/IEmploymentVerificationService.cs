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
