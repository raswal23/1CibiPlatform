namespace ATS.Features.UserManagement.Query.GetMyAccess;

public record GetMyAccessQuery : IQuery<GetMyAccessResult>;

public record GetMyAccessResult(int RoleId, int? ClientId);

public class GetMyAccessHandler : IQueryHandler<GetMyAccessQuery, GetMyAccessResult>
{
	private readonly ICurrentUser _currentUser;

	public GetMyAccessHandler(ICurrentUser currentUser)
	{
		_currentUser = currentUser;
	}

	public Task<GetMyAccessResult> Handle(
	  GetMyAccessQuery request,
	  CancellationToken cancellationToken)
	{
		if (!_currentUser.IsAuthenticated)
			throw new ForbiddenException(
				"The current user does not have valid ATS access.");

	  if (_currentUser.IsPlatformSuperAdmin)
		{
			return Task.FromResult(new GetMyAccessResult(
				AtsRoleIds.AllClients,
				null));
		}

		if (_currentUser.AtsRoleId is not > 0)
			throw new ForbiddenException(
				"The current user does not have valid ATS access.");

	  return Task.FromResult(new GetMyAccessResult(
		  _currentUser.AtsRoleId.Value,
		  _currentUser.AtsClientId));
	}
}
