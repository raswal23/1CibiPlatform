namespace ATS.Features.Report;

public record GetReportsQueryRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetReportsQueryResult>;

public record GetReportsQueryResult(PaginatedResult<ReportListDTO> Reports);

public class GetReportsQueryRequestValidator : AbstractValidator<GetReportsQueryRequest>
{
	public GetReportsQueryRequestValidator()
	{
		RuleFor(x => x.PageNumber)
			.Must(pageNumber => pageNumber is null || pageNumber > 0)
			.WithMessage("PageNumber must be greater than 0.");

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
		var paginationRequest = new PaginationRequest(
			request.PageNumber ?? 1,
			request.PageSize ?? 10,
			request.SearchTerm);

		var reports = await _reportService.GetReportsAsync(paginationRequest, cancellationToken);

		return new GetReportsQueryResult(reports);
	}
}
