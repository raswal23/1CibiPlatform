namespace Auth.Features.UserManagement.Query.GetRoles;

public record GetRolesQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetRolesQueryResult>;

public record GetRolesQueryResult(KeysetPaginatedResult<RolesDTO> Roles);

public class GetRolesQueryRequestValidator : AbstractValidator<GetRolesQueryRequest>
{
	public GetRolesQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}
public class GetRolesHandler : IQueryHandler<GetRolesQueryRequest, GetRolesQueryResult>
{
	private readonly IRoleService _roleService;

	public GetRolesHandler(IRoleService roleService)
	{
		_roleService = roleService;
	}
	public async Task<GetRolesQueryResult> Handle(GetRolesQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var roleData = await _roleService.GetRolesAsync(
			paginationRequest,
			cancellationToken);

		return new GetRolesQueryResult(roleData);
	}
}


