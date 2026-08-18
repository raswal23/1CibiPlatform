namespace FrontendWebassembly.ShareData.ATS;

public static class AtsRoleList
{
	public const int PlatformManagerId = 1;
	public const int ServiceDeliveryId = 3;

	private static readonly int[] RestrictedRoleIds = [PlatformManagerId, ServiceDeliveryId];

	public static bool IsAssignable(int roleId, bool canAssignAllRoles) =>
		canAssignAllRoles || !RestrictedRoleIds.Contains(roleId);
}
