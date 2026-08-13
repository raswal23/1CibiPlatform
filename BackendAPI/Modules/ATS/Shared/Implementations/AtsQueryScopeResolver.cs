namespace ATS.Shared.Implementations;

public sealed class AtsQueryScopeResolver
{
	private readonly ICurrentUser _currentUser;
	private readonly IUserClientRepository _userClientRepository;

	public AtsQueryScopeResolver(
		ICurrentUser currentUser,
		IUserClientRepository userClientRepository)
	{
		_currentUser = currentUser;
		_userClientRepository = userClientRepository;
	}

	public async Task<AtsQueryScope> ResolveAsync(CancellationToken cancellationToken)
	{
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return AtsQueryScope.Denied;
		}

		if (_currentUser.IsPlatformSuperAdmin)
			return AtsQueryScope.All;

		if (_currentUser.AtsRoleId is AtsRoleIds.Admin or AtsRoleIds.PlatformManager)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);

			return AtsQueryScope.ForClients(assignments.Select(assignment => assignment.ClientId));
		}

		if (_currentUser.AtsRoleId is AtsRoleIds.User or AtsRoleIds.Uploader)
			return AtsQueryScope.ForRequestor(userId);

		return _currentUser.AtsClientId is > 0
			? AtsQueryScope.ForClientAndRequestor(_currentUser.AtsClientId.Value, userId)
			: AtsQueryScope.Denied;
	}
}
