namespace ATS.Services.AccessScope;

// The one role ladder for ATS order visibility. ReportService, EndorsementSubmissionService,
// DisputeOrderService, DashboardService, the monitoring services and AtsAssistantPlugin all
// call this rather than keeping an inline copy, so a role's scope changes in one place.
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

		// Internal CIBI roles that work the whole order book rather than a client of their
		// own: Service Delivery fulfils orders it did not raise, and Client Experience
		// reviews them across every client. Both resolve to the unrestricted scope - the
		// same one a platform super admin gets above, and deliberately not the
		// client-assignment branch below, which would show them nothing until somebody
		// assigned them clients.
		if (roleId is AtsRoleIds.ServiceDelivery or AtsRoleIds.ClientExperience)
		{
			return new AtsAccessScope(null, null);
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

		if (roleId is AtsRoleIds.User
			&& _currentUser.AtsClientId is { } clientId)
		{
			return new AtsAccessScope([clientId], userId);
		}

		return null;
	}
}
