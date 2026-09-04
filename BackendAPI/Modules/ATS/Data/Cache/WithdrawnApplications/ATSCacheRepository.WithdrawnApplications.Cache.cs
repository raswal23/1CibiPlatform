namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<EmailInvitationRequestListDTO>> GetWithdrawnPageAsync(
		string? searchTerm,
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		if (afterId.HasValue)
			return await _atsRepository.GetWithdrawnPageAsync(searchTerm, afterCreatedAt, afterId, take, authorizedClientIds, requiredRequestorId, cancellationToken);

		var cacheKey = $"withdrawnapplication_ordercreated_desc_first_take_{take}_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<List<EmailInvitationRequestListDTO>>(
			cacheKey,
			async token => await _atsRepository.GetWithdrawnPageAsync(searchTerm, null, null, take, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.WithdrawnApplication],
			cancellationToken: cancellationToken);
	}

	public async Task<long> CountWithdrawnAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"withdrawnapplication_count_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<long>(
			cacheKey,
			async token => await _atsRepository.CountWithdrawnAsync(searchTerm, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.WithdrawnApplication],
			cancellationToken: cancellationToken);
	}
}
