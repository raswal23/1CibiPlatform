namespace ATS.Features.UserManagement.Query.GetUsers;

public record GetUsersQuery(PaginationRequest paginationRequest) : IQuery<GetUsersResult>;

public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
	public GetUsersQueryValidator()
	{
		RuleFor(x => x.paginationRequest)
			.NotNull().WithMessage("Pagination request is required.");

		RuleFor(x => x.paginationRequest.PageIndex)
			.GreaterThan(0).WithMessage("PageIndex must be greater than 0.");

		RuleFor(x => x.paginationRequest.PageSize)
			.GreaterThan(0).WithMessage("PageSize must be greater than 0.")
			.LessThanOrEqualTo(100).WithMessage("PageSize must be less than or equal to 100.");
	}
}

public record GetUsersResult(PaginatedResult<UserDetailsDTO> users);

public class GetUsersHandler : IQueryHandler<GetUsersQuery, GetUsersResult>
{
	private readonly IUserManagementService _userManagementService;

	public GetUsersHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<GetUsersResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
	{
		var users = await _userManagementService.GetUsersAsync(request.paginationRequest, cancellationToken);
		return new GetUsersResult(users);
	}
}
