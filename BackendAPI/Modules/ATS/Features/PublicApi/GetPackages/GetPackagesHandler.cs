namespace ATS.Features.PublicApi.GetPackages;

public record GetPackagesQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null)
	: IQuery<GetPackagesQueryResult>;

public record GetPackagesQueryResult(KeysetPaginatedResult<PackageDetailsDTO> Packages);

public class GetPackagesQueryRequestValidator : AbstractValidator<GetPackagesQueryRequest>
{
	public GetPackagesQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
	}
}

public class GetPackagesHandler : IQueryHandler<GetPackagesQueryRequest, GetPackagesQueryResult>
{
	private readonly IPackageManagementService _packageManagementService;
	private readonly ICurrentUser _currentUser;

	public GetPackagesHandler(
		IPackageManagementService packageManagementService,
		ICurrentUser currentUser)
	{
		_packageManagementService = packageManagementService;
		_currentUser = currentUser;
	}

	public async Task<GetPackagesQueryResult> Handle(
		GetPackagesQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			null,
			null);

		// The client comes from the token, never from the request: a caller must not be
		// able to read another client's entitlements by passing an id.
		var packages = await _packageManagementService.GetPackagesAsync(
			paginationRequest,
			cancellationToken,
			_currentUser.AtsClientId);

		return new GetPackagesQueryResult(packages);
	}
}
