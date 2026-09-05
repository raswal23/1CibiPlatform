namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public async Task<List<DisputeOrderListDTO>> GetDisputeOrdersPageAsync(
		string? searchTerm,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		if (afterId.HasValue)
			return await _atsRepository.GetDisputeOrdersPageAsync(searchTerm, afterCompletedAt, afterId, take, authorizedClientIds, requiredRequestorId, cancellationToken);

		var cacheKey = $"disputeorder_ordercompleted_desc_first_take_{take}_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<List<DisputeOrderListDTO>>(
			cacheKey,
			async token => await _atsRepository.GetDisputeOrdersPageAsync(searchTerm, null, null, take, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.DisputeOrder],
			cancellationToken: cancellationToken);
	}

	public async Task<long> CountDisputeOrdersAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"disputeorder_count_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<long>(
			cacheKey,
			async token => await _atsRepository.CountDisputeOrdersAsync(searchTerm, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.DisputeOrder],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.MarkAsDisputedAsync(disputeRequest, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.DisputeOrder);

		return result;
	}
}
