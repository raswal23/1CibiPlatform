namespace ATS.Features.RoleManagement.Query.GetRoles;

public record GetRolesQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null)
	: IQuery<GetRolesQueryResult>;

public record GetRolesQueryResult(KeysetPaginatedResult<RoleDetailsDTO> Roles);

public class GetRolesQueryRequestValidator : AbstractValidator<GetRolesQueryRequest>
{
	public GetRolesQueryRequestValidator()
	{
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
		var KeysetPaginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var roles = await _roleManagementService.GetRolesAsync(KeysetPaginationRequest, cancellationToken);

		return new GetRolesQueryResult(roles);
	}
}
