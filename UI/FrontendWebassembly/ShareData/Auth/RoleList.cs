namespace FrontendWebassembly.ShareData.Auth;

public static class RoleList
{
	public const int SuperAdminId = 1;

	public static Dictionary<int, string> List =>
	  new()
	  {
		{ SuperAdminId, "SuperAdmin" },
		{ 2, "Admin" },
		{ 3, "User" }
	  };
}
