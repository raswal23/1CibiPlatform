namespace ATS.Shared.Implementations;

internal static class AtsQueryScopeResolver
{
	public static AtsQueryScope Resolve(ICurrentUser currentUser)
	{
		if (currentUser.IsPlatformSuperAdmin || currentUser.AtsRoleId == AtsRoleIds.AllClients)
			return AtsQueryScope.All;

		if (currentUser.AtsRoleId == AtsRoleIds.ClientScoped && currentUser.AtsClientId is > 0)
			return AtsQueryScope.ForClient(currentUser.AtsClientId.Value);

		return currentUser.UserId is { } userId && userId != Guid.Empty
			? AtsQueryScope.ForRequestor(userId)
			: AtsQueryScope.Denied;
	}
}
