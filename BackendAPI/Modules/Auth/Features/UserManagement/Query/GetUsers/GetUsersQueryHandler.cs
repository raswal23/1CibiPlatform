namespace Auth.Features.UserManagement.Query.GetUsers;

public record GetUsersQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null) : IQuery<GetUsersQueryResult>;

public record GetUsersQueryResult(KeysetPaginatedResult<UsersDTO> Users);

public class GetUsersQueryRequestValidator : AbstractValidator<GetUsersQueryRequest>
{
	public GetUsersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}

public class GetUsersQueryHandler : IQueryHandler<GetUsersQueryRequest, GetUsersQueryResult>
{
	private readonly IUserService _userService;

	public GetUsersQueryHandler(IUserService userService)
	{
		this._userService = userService;
	}

	public async Task<GetUsersQueryResult> Handle(
		GetUsersQueryRequest request,
		CancellationToken cancellationToken)
	{

		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var userData = await _userService.GetUsersAsync(
			paginationRequest,
			cancellationToken);

		return new GetUsersQueryResult(userData);
	}
}
