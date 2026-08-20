namespace Auth.Features.UserManagement.Query.GetApplications;

public record GetApplicationsQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetApplicationsQueryResult>;

public record GetApplicationsQueryResult(KeysetPaginatedResult<ApplicationsDTO> Applications);

public class GetApplicationsQueryRequestValidator : AbstractValidator<GetApplicationsQueryRequest>
{
	public GetApplicationsQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}
public class GetApplicationsHandler : IQueryHandler<GetApplicationsQueryRequest, GetApplicationsQueryResult>
{
	private readonly IApplicationService _applicationService;

	public GetApplicationsHandler(IApplicationService applicationService)
	{
		_applicationService = applicationService;
	}
	public async Task<GetApplicationsQueryResult> Handle(GetApplicationsQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var applicationData = await _applicationService.GetApplicationsAsync(
			paginationRequest,
			cancellationToken);

		return new GetApplicationsQueryResult(applicationData);
	}
}

