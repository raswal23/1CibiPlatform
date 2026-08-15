namespace EmploymentVerification.Data.Repository;

public interface IEmploymentVerificationRepository
{
	Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns the ATS subject ids that must not be offered for a new request
	/// because they already have one awaiting a response or already confirmed.
	/// A sent request whose token has lapsed is not blocking, so the candidate
	/// can be requested again.
	/// </summary>
	Task<IReadOnlyList<Guid>> ListBlockedAtsSubjectIdsAsync(
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
	Task<bool> MarkRespondedAsync(
		Guid id,
		VerificationRequestStatus status,
		DateTime respondedAt,
		CancellationToken cancellationToken);
}
