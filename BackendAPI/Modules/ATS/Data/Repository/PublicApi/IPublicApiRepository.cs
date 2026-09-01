namespace ATS.Data.Repository.PublicApi;

public interface IPublicApiRepository
{
	/// <summary>
	/// Reads one order, filtered to the caller's scope in the query itself. Returns null
	/// when the order does not exist *or* is not the caller's, so the two are
	/// indistinguishable to an API client.
	/// </summary>
	Task<PublicOrderDetailDTO?> GetOrderAsync(
		Guid orderId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads one uploaded file's parse outcome, scoped the same way.
	/// </summary>
	Task<PublicBulkUploadStatusDTO?> GetBulkUploadStatusAsync(
		Guid fileId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Withdraws an order the caller owns. Returns false when it is already terminal or
	/// out of scope, so a stale request cannot re-withdraw a completed order.
	/// </summary>
	Task<bool> WithdrawOrderAsync(
		Guid orderId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);

	/// <summary>
	/// The order's status before a withdrawal, for the history entry.
	/// </summary>
	Task<string?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);
}
