namespace EmploymentVerification.Data.Repository;

/// <summary>
/// One employment slot of one ATS order: the unit a verification request covers.
/// </summary>
/// <remarks>
/// The pair, rather than the subject id alone, is what makes the availability check
/// correct. All three of an order's employers share an AtsSubjectId, so blocking by
/// subject would let a request raised for the first employer suppress the other two.
/// </remarks>
public readonly record struct BlockedEmploymentSegment(Guid SubjectId, short Segment);

public interface IEmploymentVerificationRepository
{
	Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns the (ATS subject, employment segment) pairs that must not be offered
	/// for a new request because they already have one awaiting a response or already
	/// confirmed. A sent request whose token has lapsed is not blocking, so that
	/// segment can be requested again.
	/// </summary>
	Task<IReadOnlyList<BlockedEmploymentSegment>> ListBlockedSegmentsAsync(
		DateTime asOfUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// ATS subject ids with at least one sent request whose link has lapsed unanswered.
	/// These segments have reopened and the order needs offering again.
	/// </summary>
	Task<IReadOnlyList<Guid>> ListSubjectsWithLapsedRequestsAsync(
		DateTime asOfUtc,
		CancellationToken cancellationToken);

	Task<EmploymentVerificationRequest?> FindByTokenHashAsync(
		string tokenHash,
		CancellationToken cancellationToken);

	Task<bool> AddAsync(
		EmploymentVerificationRequest request,
		CancellationToken cancellationToken);

	Task<bool> MarkSentAsync(
		Guid id,
		DateTime sentAt,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records the HR contact's terminal response. The matching timestamp column
	/// is set and the opposite one cleared so a row never claims both outcomes.
	/// </summary>
	/// <param name="responseNotes">
	/// What the contact said when reporting the details inaccurate. Written on every
	/// terminal response so the column always matches the outcome it sits beside - a
	/// confirmation or a lapsed send passes null and clears any earlier text, which is
	/// why a row can never carry a reason for an outcome that is not a rejection.
	/// </param>
	Task<bool> MarkRespondedAsync(
		Guid id,
		VerificationRequestStatus status,
		DateTime respondedAt,
		string? responseNotes,
		CancellationToken cancellationToken);
}
