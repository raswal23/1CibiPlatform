namespace ATS.Features.Web.UserManagement.Query.GetMyRoleId;

public record GetMyRoleIdQuery : IQuery<GetMyRoleIdResult>;

public record GetMyRoleIdResult(int? RoleId);

public class GetMyRoleIdHandler : IQueryHandler<GetMyRoleIdQuery, GetMyRoleIdResult>
{
	private readonly IUserManagementService _userManagementService;

	public GetMyRoleIdHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<GetMyRoleIdResult> Handle(
		GetMyRoleIdQuery request,
		CancellationToken cancellationToken)
	{
		var roleId = await _userManagementService.GetCurrentUserRoleIdAsync(cancellationToken);
		return new GetMyRoleIdResult(roleId);
	}
}
