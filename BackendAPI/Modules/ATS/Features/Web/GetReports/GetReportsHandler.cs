namespace ATS.Features.Web.Report;

public record GetReportsQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null, DateTime? StartDate = null, DateTime? EndDate = null)
	: IQuery<GetReportsQueryResult>;

public record GetReportsQueryResult(KeysetPaginatedResult<ReportListDTO> Reports);

public class GetReportsQueryRequestValidator : AbstractValidator<GetReportsQueryRequest>
{
	public GetReportsQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
	}
}

public class GetReportsHandler : IQueryHandler<GetReportsQueryRequest, GetReportsQueryResult>
{
	private readonly IReportService _reportService;

	public GetReportsHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<GetReportsQueryResult> Handle(GetReportsQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		var reports = await _reportService.GetReportsAsync(paginationRequest, cancellationToken);

		return new GetReportsQueryResult(reports);
	}
}
