namespace ATS.Features.DisputeOrder;

public record GetDisputeOrdersQueryRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetDisputeOrdersQueryResult>;

public record GetDisputeOrdersQueryResult(PaginatedResult<DisputeOrderListDTO> Orders);

public class GetDisputeOrdersQueryRequestValidator : AbstractValidator<GetDisputeOrdersQueryRequest>
{
	public GetDisputeOrdersQueryRequestValidator()
	{
		RuleFor(x => x.PageNumber)
			.Must(pageNumber => pageNumber is null || pageNumber > 0)
			.WithMessage("PageNumber must be greater than 0.");

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
		var paginationRequest = new PaginationRequest(
			request.PageNumber ?? 1,
			request.PageSize ?? 10,
			request.SearchTerm);

		var orders = await _disputeOrderService.GetDisputeOrdersAsync(paginationRequest, cancellationToken);

		return new GetDisputeOrdersQueryResult(orders);
	}
}
