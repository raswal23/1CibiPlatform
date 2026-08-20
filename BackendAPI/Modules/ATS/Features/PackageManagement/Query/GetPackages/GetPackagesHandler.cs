namespace ATS.Features.PackageManagement.Query.GetPackages;

public record GetPackagesQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null, int? ClientId = null)
	: IQuery<GetPackagesQueryResult>;

public record GetPackagesQueryResult(KeysetPaginatedResult<PackageDetailsDTO> Packages);

public class GetPackagesQueryRequestValidator : AbstractValidator<GetPackagesQueryRequest>
{
	public GetPackagesQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		RuleFor(x => x.ClientId)
			.Must(clientId => clientId is null || clientId > 0)
			.WithMessage("ClientId must be greater than 0.");
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
		var KeysetPaginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var packages = await _packageManagementService.GetPackagesAsync(KeysetPaginationRequest, cancellationToken, request.ClientId);

		return new GetPackagesQueryResult(packages);
	}
}
