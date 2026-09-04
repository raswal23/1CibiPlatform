namespace ATS.Data.Repository;

public interface IDisputeOrderRepository
{
	Task<List<DisputeOrderListDTO>> GetDisputeOrdersPageAsync(
		string? searchTerm,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<long> CountDisputeOrdersAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken);
}
