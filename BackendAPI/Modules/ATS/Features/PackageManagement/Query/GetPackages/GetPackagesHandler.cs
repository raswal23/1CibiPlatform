namespace ATS.Features.PackageManagement.Query.GetPackages;

public record GetPackagesQueryRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetPackagesQueryResult>;

public record GetPackagesQueryResult(PaginatedResult<PackageDetailsDTO> Packages);

public class GetPackagesQueryRequestValidator : AbstractValidator<GetPackagesQueryRequest>
{
	public GetPackagesQueryRequestValidator()
	{
		RuleFor(x => x.PageNumber)
			.Must(pageNumber => pageNumber is null || pageNumber > 0)
			.WithMessage("PageNumber must be greater than 0.");

		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
	}
}

public class GetPackagesHandler : IQueryHandler<GetPackagesQueryRequest, GetPackagesQueryResult>
{
	private readonly IPackageManagementService _packageManagementService;

	public GetPackagesHandler(IPackageManagementService packageManagementService)
	{
		_packageManagementService = packageManagementService;
	}

	public async Task<GetPackagesQueryResult> Handle(GetPackagesQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new PaginationRequest(
			request.PageNumber ?? 1,
			request.PageSize ?? 10,
			request.SearchTerm);

		var packages = await _packageManagementService.GetPackagesAsync(paginationRequest, cancellationToken);

		return new GetPackagesQueryResult(packages);
	}
}
