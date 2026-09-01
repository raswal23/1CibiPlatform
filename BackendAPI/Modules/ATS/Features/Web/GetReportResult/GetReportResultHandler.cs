namespace ATS.Features.Web.Report;

public record GetReportResultQueryRequest(Guid EmailInvitationRequestId) : IQuery<GetReportResultQueryResult>;

public record GetReportResultQueryResult(ReportResultDTO ReportResult);

public class GetReportResultQueryRequestValidator : AbstractValidator<GetReportResultQueryRequest>
{
	public GetReportResultQueryRequestValidator()
	{
		RuleFor(x => x.EmailInvitationRequestId)
			.NotEmpty()
			.WithMessage("Email invitation request ID is required.");
	}
}

public class GetReportResultHandler : IQueryHandler<GetReportResultQueryRequest, GetReportResultQueryResult>
{
	private readonly IReportService _reportService;

	public GetReportResultHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<GetReportResultQueryResult> Handle(GetReportResultQueryRequest request, CancellationToken cancellationToken)
	{
		var reportResult = await _reportService.GetReportResultByEmailInvitationRequestIdAsync(request.EmailInvitationRequestId, cancellationToken);
		return new GetReportResultQueryResult(reportResult);
	}
}
