namespace EmploymentVerification.Data.Repository;

public sealed class EmploymentVerificationRepository(EmploymentVerificationDbContext db)
	: IEmploymentVerificationRepository
{
	public async Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(CancellationToken cancellationToken) =>
		await db.Requests.AsNoTracking()
			.OrderByDescending(request => request.RequestedAt)
			.ToListAsync(cancellationToken);

	public Task<EmploymentVerificationRequest?> FindByTokenHashAsync(
		string tokenHash,
		CancellationToken cancellationToken) =>
		db.Requests.SingleOrDefaultAsync(request => request.VerificationTokenHash == tokenHash, cancellationToken);

	public Task AddAsync(EmploymentVerificationRequest request, CancellationToken cancellationToken) =>
		db.Requests.AddAsync(request, cancellationToken).AsTask();

	public async Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException exception)
		{
			var databaseMessage = exception.GetBaseException().Message;

			throw new InvalidOperationException(
				$"Employment Verification could not be saved: {databaseMessage}",
				exception);
		}
	}
}
