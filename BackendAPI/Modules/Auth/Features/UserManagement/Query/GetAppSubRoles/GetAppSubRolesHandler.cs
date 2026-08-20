namespace Auth.Features.UserManagement.Query.GetAppSubRoles;

public record GetAppSubRolesQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetAppSubRolesQueryResult>;

public record GetAppSubRolesQueryResult(KeysetPaginatedResult<AppSubRolesDTO> AppSubRoles);

public class GetAppSubRolesQueryRequestValidator : AbstractValidator<GetAppSubRolesQueryRequest>
{
	public GetAppSubRolesQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}
public class GetAppSubRolesHandler : IQueryHandler<GetAppSubRolesQueryRequest, GetAppSubRolesQueryResult>
{
	private readonly IAppSubRoleService _appSubRoleService;

	public GetAppSubRolesHandler(IAppSubRoleService appSubRoleService)
	{
		_appSubRoleService = appSubRoleService;
	}
	public async Task<GetAppSubRolesQueryResult> Handle(GetAppSubRolesQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var appSubRoleData = await _appSubRoleService.GetAppSubRolesAsync(
			paginationRequest,
			cancellationToken);

		return new GetAppSubRolesQueryResult(appSubRoleData);
	}
}


