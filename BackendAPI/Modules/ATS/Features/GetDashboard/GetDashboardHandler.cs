namespace ATS.Features.Dashboard;

public record GetDashboardQueryRequest(string? Requester) : IQuery<GetDashboardQueryResult>;

public record GetDashboardQueryResult(ATSDashboardDTO Dashboard);

public class GetDashboardQueryRequestValidator : AbstractValidator<GetDashboardQueryRequest>
{
	public GetDashboardQueryRequestValidator()
	{
		RuleFor(x => x.Requester)
			.MaximumLength(255)
			.When(x => !string.IsNullOrWhiteSpace(x.Requester));
	}
}

public class GetDashboardHandler : IQueryHandler<GetDashboardQueryRequest, GetDashboardQueryResult>
{
	private readonly IDashboardService _dashboardService;

	public GetDashboardHandler(IDashboardService dashboardService)
	{
		_dashboardService = dashboardService;
	}

	public async Task<GetDashboardQueryResult> Handle(
		GetDashboardQueryRequest request,
		CancellationToken cancellationToken)
	{
		var dashboard = await _dashboardService.GetDashboardAsync(request.Requester, cancellationToken);
		return new GetDashboardQueryResult(dashboard);
	}
}
