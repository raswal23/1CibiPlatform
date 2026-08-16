namespace EmploymentVerification.Data.Cache;

public sealed class EmploymentVerificationCacheRepository(
	IEmploymentVerificationRepository repository,
	HybridCache cache) : IEmploymentVerificationRepository
{
	private const string RequestsKey = "employmentverification:requests";
	private const string RequestsTag = "employmentverification:requests";

	public Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(
		CancellationToken cancellationToken) =>
		cache.GetOrCreateAsync(
			RequestsKey,
			async _ => await repository.ListAsync(cancellationToken),
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(2) },
			[RequestsTag],
			cancellationToken).AsTask();

	// Deliberately uncached: the result turns on how the supplied instant compares
	// to each token expiry, so a cached list would keep lapsed requests blocking
	// their candidate until the entry aged out.
	public Task<IReadOnlyList<Guid>> ListBlockedAtsSubjectIdsAsync(
		DateTime asOfUtc,
		CancellationToken cancellationToken) =>
		repository.ListBlockedAtsSubjectIdsAsync(asOfUtc, cancellationToken);

	public Task<EmploymentVerificationRequest?> FindByTokenHashAsync(
		string tokenHash,
		CancellationToken cancellationToken) =>
		repository.FindByTokenHashAsync(tokenHash, cancellationToken);

	public async Task<bool> AddAsync(
		EmploymentVerificationRequest request,
		CancellationToken cancellationToken)
	{
		var result = await repository.AddAsync(request, cancellationToken);

		if (result)
		{
			await cache.RemoveByTagAsync(RequestsTag, cancellationToken);
		}

		return result;
	}

	public async Task<bool> MarkSentAsync(
		Guid id,
		DateTime sentAt,
		CancellationToken cancellationToken)
	{
		var result = await repository.MarkSentAsync(id, sentAt, cancellationToken);

		if (result)
		{
			await cache.RemoveByTagAsync(RequestsTag, cancellationToken);
		}

		return result;
	}

	public async Task<bool> MarkRespondedAsync(
		Guid id,
		VerificationRequestStatus status,
		DateTime respondedAt,
		CancellationToken cancellationToken)
	{
		var result = await repository.MarkRespondedAsync(
			id,
			status,
			respondedAt,
			cancellationToken);

		if (result)
		{
			await cache.RemoveByTagAsync(RequestsTag, cancellationToken);
		}

		return result;
	}
}
