namespace ATS.Features.Web.ModuleManagement.Query.GetModules;

public record GetModulesQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetModulesQueryResult>;

public record GetModulesQueryResult(KeysetPaginatedResult<ModuleDetailsDTO> Modules);

public class GetModulesQueryRequestValidator : AbstractValidator<GetModulesQueryRequest>
{
	public GetModulesQueryRequestValidator()
	{
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
		var KeysetPaginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var modules = await _moduleManagementService.GetModulesAsync(KeysetPaginationRequest, cancellationToken);

		return new GetModulesQueryResult(modules);
	}
}
