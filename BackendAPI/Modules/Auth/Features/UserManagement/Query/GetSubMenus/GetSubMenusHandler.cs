namespace Auth.Features.UserManagement.Query.GetSubMenus;

public record GetSubMenusQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetSubMenusQueryResult>;

public record GetSubMenusQueryResult(KeysetPaginatedResult<SubMenusDTO> subMenus);

public class GetSubMenusQueryRequestValidator : AbstractValidator<GetSubMenusQueryRequest>
{
	public GetSubMenusQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}
public class GetSubMenusHandler : IQueryHandler<GetSubMenusQueryRequest, GetSubMenusQueryResult>
{
	private readonly ISubMenuService _subMenuService;

	public GetSubMenusHandler(ISubMenuService subMenuService)
	{
		_subMenuService = subMenuService;
	}
	public async Task<GetSubMenusQueryResult> Handle(GetSubMenusQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var subMenuData = await _subMenuService.GetSubMenusAsync(
			paginationRequest,
			cancellationToken);

		return new GetSubMenusQueryResult(subMenuData);
	}
}

