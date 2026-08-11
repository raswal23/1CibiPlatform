namespace ATS.Features.RoleManagement.Query.GetRoles;

public record GetRolesQueryRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetRolesQueryResult>;

public record GetRolesQueryResult(PaginatedResult<RoleDetailsDTO> Roles);

public class GetRolesQueryRequestValidator : AbstractValidator<GetRolesQueryRequest>
{
	public GetRolesQueryRequestValidator()
	{
		RuleFor(x => x.PageNumber)
			.Must(pageNumber => pageNumber is null || pageNumber > 0)
			.WithMessage("PageNumber must be greater than 0.");

		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");
	}
}

public class GetRolesHandler : IQueryHandler<GetRolesQueryRequest, GetRolesQueryResult>
{
	private readonly IRoleManagementService _roleManagementService;

	public GetRolesHandler(IRoleManagementService roleManagementService)
	{
		_roleManagementService = roleManagementService;
	}

	public async Task<GetRolesQueryResult> Handle(GetRolesQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new PaginationRequest(
			request.PageNumber ?? 1,
			request.PageSize ?? 10,
			request.SearchTerm);

		var roles = await _roleManagementService.GetRolesAsync(paginationRequest, cancellationToken);

		return new GetRolesQueryResult(roles);
	}
}
