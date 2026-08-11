namespace ATS.Features.UserManagement.Query.GetAuthUsers;

public record GetAuthUsersQuery : IQuery<GetAuthUsersResult>;

public record GetAuthUsersResult(IReadOnlyList<ATSUserLookupDTO> users);

public class GetAuthUsersHandler : IQueryHandler<GetAuthUsersQuery, GetAuthUsersResult>
{
	private readonly IUserManagementService _userManagementService;

	public GetAuthUsersHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<GetAuthUsersResult> Handle(
		GetAuthUsersQuery request,
		CancellationToken cancellationToken)
	{
		var users = await _userManagementService.GetAuthUsersAsync(cancellationToken);
		return new GetAuthUsersResult(users);
	}
}
