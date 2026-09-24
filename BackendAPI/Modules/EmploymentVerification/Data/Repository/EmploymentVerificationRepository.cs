namespace EmploymentVerification.Data.Repository;

public sealed class EmploymentVerificationRepository(EmploymentVerificationDbContext db)
	: IEmploymentVerificationRepository
{
	public async Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(
		CancellationToken cancellationToken) =>
		await db.Requests.AsNoTracking()
			.OrderByDescending(request => request.RequestedAt)
			.ToListAsync(cancellationToken);

	/// <summary>
	/// The availability rule. A segment is blocked while a request for it is awaiting a
	/// response, and permanently once the employer has answered either way.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>Rejected</c> blocks. It did not under manual sending, where it meant "an
	/// operator may try this employer again" and a human decided whether to. With a job
	/// sending every five minutes that reading re-mails an employer who has just
	/// declined, which is the one outcome a verification flow must never produce.
	/// </para>
	/// <para>
	/// The cost is that a send failure - which also lands on <c>Rejected</c>, to release
	/// the committed row - is no longer retried automatically. That is the safer side of
	/// the trade: a missed send is visible in the queue and can be resent by hand, while
	/// a duplicate request to someone who said no cannot be taken back.
	/// </para>
	/// </remarks>
	public async Task<IReadOnlyList<BlockedEmploymentSegment>> ListBlockedSegmentsAsync(
		DateTime asOfUtc,
		CancellationToken cancellationToken) =>
		await db.Requests.AsNoTracking()
			.Where(request => request.AtsSubjectId != null)
			.Where(request => request.EmploymentSegment != null)
			.Where(request =>
				request.Status == VerificationRequestStatus.Pending ||
				request.Status == VerificationRequestStatus.Verified ||
				request.Status == VerificationRequestStatus.Rejected ||
				(request.Status == VerificationRequestStatus.Sent &&
					request.TokenExpiresAt >= asOfUtc))
			.Select(request => new BlockedEmploymentSegment(
				request.AtsSubjectId!.Value,
				request.EmploymentSegment!.Value))
			.Distinct()
			.ToListAsync(cancellationToken);

	// The mirror of the Sent clause in the availability rule above: a request whose
	// link has passed its expiry without an answer releases its segment, which means
	// the order has work to do again and must be offered by ATS once more.
	public async Task<IReadOnlyList<Guid>> ListSubjectsWithLapsedRequestsAsync(
		DateTime asOfUtc,
		CancellationToken cancellationToken) =>
		await db.Requests.AsNoTracking()
			.Where(request => request.AtsSubjectId != null)
			.Where(request => request.Status == VerificationRequestStatus.Sent)
			.Where(request => request.TokenExpiresAt < asOfUtc)
			.Select(request => request.AtsSubjectId!.Value)
			.Distinct()
			.ToListAsync(cancellationToken);

	public Task<EmploymentVerificationRequest?> FindByTokenHashAsync(
		string tokenHash,
		CancellationToken cancellationToken) =>
		db.Requests.SingleOrDefaultAsync(
			request => request.VerificationTokenHash == tokenHash,
			cancellationToken);

	public async Task<bool> AddAsync(
		EmploymentVerificationRequest request,
		CancellationToken cancellationToken)
	{
		await db.Requests.AddAsync(request, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);

		return true;
	}

	public async Task<bool> MarkSentAsync(
		Guid id,
		DateTime sentAt,
		CancellationToken cancellationToken)
	{
		var affectedRows = await db.Requests
			.Where(request => request.Id == id)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(request => request.Status, VerificationRequestStatus.Sent)
					.SetProperty(request => request.SentAt, sentAt),
				cancellationToken);

		return affectedRows > 0;
	}

	public async Task<bool> MarkRespondedAsync(
		Guid id,
		VerificationRequestStatus status,
		DateTime respondedAt,
		CancellationToken cancellationToken)
	{
		var verifiedAt = status == VerificationRequestStatus.Verified
			? respondedAt
			: (DateTime?)null;
		var rejectedAt = status == VerificationRequestStatus.Rejected
			? respondedAt
			: (DateTime?)null;

		// Single use is enforced here rather than by the prior read: restricting
		// the update to a non-terminal row means two simultaneous clicks cannot
		// both record a response.
		var affectedRows = await db.Requests
			.Where(request => request.Id == id)
			.Where(request =>
				request.Status == VerificationRequestStatus.Pending ||
				request.Status == VerificationRequestStatus.Sent)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(request => request.Status, status)
					.SetProperty(request => request.VerifiedAt, verifiedAt)
					.SetProperty(request => request.RejectedAt, rejectedAt),
				cancellationToken);

		return affectedRows > 0;
	}
}
