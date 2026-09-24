namespace EmploymentVerification.Services;

/// <param name="EmploymentSegment">
/// Which of the application form's three employer slots this request covers. Null for
/// a request not raised from an ATS record. Stored on the row so the availability
/// check can tell an order's three employers apart.
/// </param>
public sealed record CreateEmploymentVerificationRequest(
	string CandidateName,
	string PreviousEmployer,
	string Position,
	string HrEmail,
	DateTime? EmploymentStartDate,
	DateTime? EmploymentEndDate,
	Guid? AtsSubjectId,
	short? EmploymentSegment = null,
	string? RecipientSource = null);

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

	/// <summary>
	/// Tells ATS these orders no longer need handing over, so later passes skip them.
	/// </summary>
	/// <remarks>
	/// Only call this for an order with nothing outstanding. A released order is
	/// invisible to <see cref="GetAvailableATSRecordsAsync"/> until something reinstates
	/// it, so releasing one that still has work to do would strand that work silently.
	/// </remarks>
	Task ReleaseFinishedOrdersAsync(
		IReadOnlyCollection<Guid> subjectIds,
		CancellationToken cancellationToken);

	/// <summary>
	/// Puts back any released order that has become actionable again, so the next read
	/// can see it.
	/// </summary>
	/// <remarks>
	/// The one way that happens is a sent link lapsing unanswered: the segment reopens
	/// here, but ATS was told the order was finished and stopped offering it. This is
	/// what reconciles the two.
	/// </remarks>
	Task ReinstateLapsedOrdersAsync(CancellationToken cancellationToken);
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
