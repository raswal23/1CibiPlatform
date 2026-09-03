namespace ATS.Features.Web.DisputeOrder;

public record GetDisputeOrdersQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetDisputeOrdersQueryResult>;

public record GetDisputeOrdersQueryResult(KeysetPaginatedResult<DisputeOrderListDTO> Orders);

public class GetDisputeOrdersQueryRequestValidator : AbstractValidator<GetDisputeOrdersQueryRequest>
{
	public GetDisputeOrdersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
	}
}

public class GetDisputeOrdersHandler : IQueryHandler<GetDisputeOrdersQueryRequest, GetDisputeOrdersQueryResult>
{
	private readonly IDisputeOrderService _disputeOrderService;

	public GetDisputeOrdersHandler(IDisputeOrderService disputeOrderService)
	{
		_disputeOrderService = disputeOrderService;
	}

	public async Task<GetDisputeOrdersQueryResult> Handle(GetDisputeOrdersQueryRequest request, CancellationToken cancellationToken)
	{
		var KeysetPaginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var orders = await _disputeOrderService.GetDisputeOrdersAsync(KeysetPaginationRequest, cancellationToken);

		return new GetDisputeOrdersQueryResult(orders);
	}
}
