namespace ATS.Features.Web.UserManagement.Query.GetMyModules;

public record GetMyModulesQuery(Guid UserId) : IQuery<GetMyModulesResult>;

public record GetMyModulesResult(IReadOnlyList<int> ModuleIds);

public class GetMyModulesHandler : IQueryHandler<GetMyModulesQuery, GetMyModulesResult>
{
	private readonly IUserManagementService _userManagementService;

	public GetMyModulesHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<GetMyModulesResult> Handle(
		GetMyModulesQuery request,
		CancellationToken cancellationToken)
	{
		var moduleIds = await _userManagementService.GetActiveUserModuleIdsAsync(
			request.UserId,
			cancellationToken);
		return new GetMyModulesResult(moduleIds);
	}
}
