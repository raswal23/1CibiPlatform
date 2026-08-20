namespace ATS.Features.ModuleManagement.Query.GetModules;

public record GetModulesQueryRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetModulesQueryResult>;

public record GetModulesQueryResult(PaginatedResult<ModuleDetailsDTO> Modules);

public class GetModulesQueryRequestValidator : AbstractValidator<GetModulesQueryRequest>
{
	public GetModulesQueryRequestValidator()
	{
		RuleFor(x => x.PageNumber)
			.Must(pageNumber => pageNumber is null || pageNumber > 0)
			.WithMessage("PageNumber must be greater than 0.");

		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
	}
}

public class GetModulesHandler : IQueryHandler<GetModulesQueryRequest, GetModulesQueryResult>
{
	private readonly IModuleManagementService _moduleManagementService;

	public GetModulesHandler(IModuleManagementService moduleManagementService)
	{
		_moduleManagementService = moduleManagementService;
	}

	public async Task<GetModulesQueryResult> Handle(GetModulesQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new PaginationRequest(
			request.PageNumber ?? 1,
			request.PageSize ?? 10,
			request.SearchTerm);

		var modules = await _moduleManagementService.GetModulesAsync(paginationRequest, cancellationToken);

		return new GetModulesQueryResult(modules);
	}
}
