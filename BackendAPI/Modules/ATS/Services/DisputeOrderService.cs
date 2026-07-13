namespace ATS.Services;

public class DisputeOrderService : IDisputeOrderService
{
	private readonly ILogger<DisputeOrderService> _logger;
	private readonly IATSRepository _atsRepository;

	public DisputeOrderService(
		ILogger<DisputeOrderService> logger,
		IATSRepository atsRepository)
	{
		_logger = logger;
		_atsRepository = atsRepository;
	}

	public Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetDisputeOrders",
			Step = "FetchingDisputeOrders",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching dispute orders with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
				_atsRepository.GetDisputeOrdersAsync(paginationRequest, cancellationToken) :
				_atsRepository.SearchDisputeOrdersAsync(paginationRequest, cancellationToken);
	}
}
