namespace EmploymentVerification.Data.Repository;

public sealed class EmploymentVerificationRepository(EmploymentVerificationDbContext db)
	: IEmploymentVerificationRepository
{
	public async Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(
		CancellationToken cancellationToken) =>
		await db.Requests.AsNoTracking()
			.OrderByDescending(request => request.RequestedAt)
			.ToListAsync(cancellationToken);

	public async Task<IReadOnlyList<Guid>> ListBlockedAtsSubjectIdsAsync(
		DateTime asOfUtc,
		CancellationToken cancellationToken) =>
		await db.Requests.AsNoTracking()
			.Where(request => request.AtsSubjectId != null)
			.Where(request =>
				request.Status == VerificationRequestStatus.Pending ||
				request.Status == VerificationRequestStatus.Verified ||
				(request.Status == VerificationRequestStatus.Sent &&
					request.TokenExpiresAt >= asOfUtc))
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
