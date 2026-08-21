namespace ATS.Services.AccessScope;

// The same role ladder ReportService.GetReportsAsync applies inline. Extracted here so
// new features do not add another copy of it. The existing inline copies in
// ReportService, EndorsementSubmissionService, DisputeOrderService, DashboardService and
// AtsAssistantPlugin are intentionally left alone - converting them is a separate,
// behaviour-preserving change.
public sealed class AtsAccessScopeResolver : IAtsAccessScopeResolver
{
	private readonly ICurrentUser _currentUser;
	private readonly IUserClientRepository _userClientRepository;

	public AtsAccessScopeResolver(
		ICurrentUser currentUser,
		IUserClientRepository userClientRepository)
	{
		_currentUser = currentUser;
		_userClientRepository = userClientRepository;
	}

	public async Task<AtsAccessScope?> ResolveAsync(CancellationToken cancellationToken)
	{
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return null;
		}

		if (_currentUser.IsPlatformSuperAdmin)
		{
			return new AtsAccessScope(null, null);
		}

		if (_currentUser.AtsRoleId is not { } roleId)
		{
			return null;
		}

		if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);

			var clientIds = assignments
				.Select(assignment => assignment.ClientId)
				.Distinct()
				.ToArray();

			return new AtsAccessScope(clientIds, null);
		}

		if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			&& _currentUser.AtsClientId is { } clientId)
		{
			return new AtsAccessScope([clientId], userId);
		}

		return null;
	}
}
