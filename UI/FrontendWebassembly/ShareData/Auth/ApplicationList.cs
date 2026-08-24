namespace FrontendWebassembly.ShareData.Auth;

public static class ApplicationListDescriptionIcon
{
	public static Dictionary<int, (string path, string Name, string Icon, string Subtitle)> List => new()
	{
		{ 1, ("cnx","CNX", Icons.Material.Filled.Phone, "Open this application to continue your work.") },
		{ 2, ("philsys","PhilSys", Icons.Material.Filled.Flag, "Look up and verify registrants using PSA national ID data.") },
		{ 3, ("settings", "Settings", Icons.Material.Filled.Settings, "Manage users, roles, permissions, and platform-wide preferences.") },
		{ 4, ("ai", "AI", Icons.Material.Filled.Android, "Confirm work history and credentials for candidate records.") },
		{ 5, ("creditbureau", "Credit Bureau", Icons.Material.Filled.AccountBalance, "Open this application to continue your work.") },
		{ 6, ("s&i", "S&I", Icons.Material.Filled.Apps, "Screening & investigation console — manage candidate orders, reports, and disputes.") },
		{ 7, ("administration", "Administration", Icons.Material.Filled.AdminPanelSettings, "Review application logs and monitor activity across the platform.") },
		{ 8, ("employmentverification", "Employment Verification", Icons.Material.Filled.VerifiedUser, "Verify candidate employment records and work history.") }
	};

}
