namespace ATS.Services.PublicApi;

public sealed class PublicApiService : IPublicApiService
{
	private readonly ILogger<PublicApiService> _logger;
	private readonly IPublicApiRepository _repository;
	private readonly IAtsAccessScopeResolver _scopeResolver;
	private readonly IOrderHistoryService _orderHistoryService;

	public PublicApiService(
		ILogger<PublicApiService> logger,
		IPublicApiRepository repository,
		IAtsAccessScopeResolver scopeResolver,
		IOrderHistoryService orderHistoryService)
	{
		_logger = logger;
		_repository = repository;
		_scopeResolver = scopeResolver;
		_orderHistoryService = orderHistoryService;
	}

	public async Task<PublicOrderDetailDTO> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
	{
		var accessScope = await ResolveScopeAsync(cancellationToken);

		var order = await _repository.GetOrderAsync(
			orderId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		return order ?? throw NotFound(orderId);
	}

	public async Task<PublicBulkUploadStatusDTO> GetBulkUploadStatusAsync(Guid fileId, CancellationToken cancellationToken)
	{
		var accessScope = await ResolveScopeAsync(cancellationToken);

		var status = await _repository.GetBulkUploadStatusAsync(
			fileId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		return status ?? throw new NotFoundException($"Bulk upload with ID {fileId} not found.");
	}

	public async Task<bool> WithdrawOrderAsync(Guid orderId, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "PublicWithdrawOrder",
			Step = "Withdrawing",
			OrderId = orderId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Withdrawing an order through the public API: {@Context}", logContext);

		var accessScope = await ResolveScopeAsync(cancellationToken);

		// Read first only to distinguish "not yours" from "already terminal": the write
		// below re-applies the scope, so this read is not what secures the operation.
		var order = await _repository.GetOrderAsync(
			orderId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		if (order is null)
		{
			_logger.LogWarning("Withdraw denied for an unknown or out-of-scope order: {@Context}", logContext);

			throw NotFound(orderId);
		}

		var previousStatus = order.OrderStatus;

		var withdrawn = await _repository.WithdrawOrderAsync(
			orderId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		if (!withdrawn)
		{
			_logger.LogWarning("Withdraw rejected, the order is already terminal: {@Context}", logContext);

			throw new ConflictException(
				"This order can no longer be withdrawn. It is already withdrawn or completed.");
		}

		await _orderHistoryService.RecordAsync(
			orderId,
			OrderHistoryEventType.ApplicationFormWithdrawn,
			previousStatus,
			OrderStatus.ApplicationWithdrawn,
			cancellationToken,
			OrderHistorySource.PublicApi);

		_logger.LogInformation("Order withdrawn through the public API: {@Context}", logContext);

		return true;
	}

	private async Task<AtsAccessScope> ResolveScopeAsync(CancellationToken cancellationToken) =>
		await _scopeResolver.ResolveAsync(cancellationToken)
			?? throw new ForbiddenException("The access token does not grant ATS access.");

	// Out of scope reads as not found, never forbidden: a 403 would confirm that an
	// order belonging to another client exists.
	private static NotFoundException NotFound(Guid orderId) =>
		new($"Order with ID {orderId} not found.");
}
