namespace Auth.Features.UserManagement.Query.GetLockedUsers;
public record GetLockedUsersQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetLockedUsersQueryResult>;

public record GetLockedUsersQueryResult(KeysetPaginatedResult<AuthAttempts> LockedUsers);
public class GetLockedUsersQueryRequestValidator : AbstractValidator<GetLockedUsersQueryRequest>
{
	public GetLockedUsersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be between 1 and 100.");
	}
}
public class GetLockedUsersHandler : IQueryHandler<GetLockedUsersQueryRequest, GetLockedUsersQueryResult>
{
	private readonly ILockerUserService _lockedUserService;
	public GetLockedUsersHandler(ILockerUserService lockedUserService)
	{
		_lockedUserService = lockedUserService;
	}
	public async Task<GetLockedUsersQueryResult> Handle(GetLockedUsersQueryRequest request, CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var lockedUserData = await _lockedUserService.GetLockedUsersAsync(
			paginationRequest,
			cancellationToken);

		return new GetLockedUsersQueryResult(lockedUserData);
	}
}

