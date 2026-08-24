namespace Auth.Features.UserManagement.Query.GetUnApprovedUsers;
public record GetUnApprovedUsersQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null) : IQuery<GetUnApprovedUsersQueryResult>;

public record GetUnApprovedUsersQueryResult(KeysetPaginatedResult<UsersDTO> Users);

public class GetUnApprovedUsersQueryRequestValidator : AbstractValidator<GetUnApprovedUsersQueryRequest>
{
	public GetUnApprovedUsersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}
public class GetUnApprovedUsersHandler : IQueryHandler<GetUnApprovedUsersQueryRequest, GetUnApprovedUsersQueryResult>
{
	private readonly IUserService _userService;
	public GetUnApprovedUsersHandler(IUserService userService)
	{
		_userService = userService;
	}
	public async Task<GetUnApprovedUsersQueryResult> Handle(
		GetUnApprovedUsersQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);
		var userData = await _userService.GetUnApprovedUsersAsync(
			paginationRequest,
			cancellationToken);
		return new GetUnApprovedUsersQueryResult(userData);
	}
}
