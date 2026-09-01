namespace ATS.Services.PublicApi;

public interface IPublicApiService
{
	/// <summary>
	/// Reads one order. Throws NotFoundException when it is unknown or outside the
	/// caller's scope - the two must be indistinguishable, or the API would confirm
	/// that another client's order exists.
	/// </summary>
	Task<PublicOrderDetailDTO> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

	Task<PublicBulkUploadStatusDTO> GetBulkUploadStatusAsync(Guid fileId, CancellationToken cancellationToken);

	/// <summary>
	/// Withdraws an order the caller owns. Throws NotFoundException when it is unknown
	/// or out of scope, and ConflictException when it is already withdrawn or completed.
	/// </summary>
	Task<bool> WithdrawOrderAsync(Guid orderId, CancellationToken cancellationToken);
}
