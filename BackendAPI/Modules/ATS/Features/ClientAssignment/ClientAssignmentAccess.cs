namespace ATS.Features.ClientAssignment;

internal static class ClientAssignmentAccess
{
	private const int UserManagementModuleId = 10;

	public static async Task<bool> CanManageAsync(
		HttpContext httpContext,
		IUserManagementService userManagementService,
		CancellationToken cancellationToken)
	{
		var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
			?? httpContext.User.FindFirstValue("userId");
		if (!Guid.TryParse(userIdValue, out var userId))
			return false;

		var moduleIds = await userManagementService.GetActiveUserModuleIdsAsync(
			userId,
			cancellationToken);
		return moduleIds.Contains(UserManagementModuleId);
	}
}
