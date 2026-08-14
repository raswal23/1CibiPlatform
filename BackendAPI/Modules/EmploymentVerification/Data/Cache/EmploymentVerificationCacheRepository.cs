namespace EmploymentVerification.Data.Cache;

public sealed class EmploymentVerificationCacheRepository(
	IEmploymentVerificationRepository repository,
	HybridCache cache) : IEmploymentVerificationRepository
{
	private const string RequestsKey = "employmentverification:requests";
	private const string RequestsTag = "employmentverification:requests";

	public Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(CancellationToken cancellationToken) =>
		cache.GetOrCreateAsync(
			RequestsKey,
			async _ => await repository.ListAsync(cancellationToken),
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(2) },
			[RequestsTag],
			cancellationToken).AsTask();

	public Task<EmploymentVerificationRequest?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
		repository.FindByTokenHashAsync(tokenHash, cancellationToken);

	public Task AddAsync(EmploymentVerificationRequest request, CancellationToken cancellationToken) =>
		repository.AddAsync(request, cancellationToken);

	public async Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		await repository.SaveChangesAsync(cancellationToken);
		await cache.RemoveByTagAsync(RequestsTag, cancellationToken);
	}
}
