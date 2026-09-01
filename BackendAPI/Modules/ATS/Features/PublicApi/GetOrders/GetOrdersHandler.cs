namespace ATS.Features.PublicApi.GetOrders;

public record GetOrdersQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetOrdersQueryResult>;

public record GetOrdersQueryResult(KeysetPaginatedResult<ReportListDTO> Orders);

public class GetOrdersQueryRequestValidator : AbstractValidator<GetOrdersQueryRequest>
{
	public GetOrdersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		RuleFor(x => x.EndDate)
			.GreaterThanOrEqualTo(x => x.StartDate!.Value)
			.When(x => x.StartDate.HasValue && x.EndDate.HasValue)
			.WithMessage("EndDate must not be earlier than StartDate.");
	}
}

public class GetOrdersHandler : IQueryHandler<GetOrdersQueryRequest, GetOrdersQueryResult>
{
	private readonly IReportService _reportService;

	public GetOrdersHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<GetOrdersQueryResult> Handle(
		GetOrdersQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		// GetReportsAsync resolves the caller's scope itself, so a token scoped to one
		// client can only ever see that client's orders.
		var orders = await _reportService.GetReportsAsync(paginationRequest, cancellationToken);

		return new GetOrdersQueryResult(orders);
	}
}
