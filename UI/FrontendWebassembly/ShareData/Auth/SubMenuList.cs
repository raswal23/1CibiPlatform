namespace FrontendWebassembly.ShareData.Auth;

public static class SubMenuList
{
	public static Dictionary<int, (string path, string Name, string Icon)> List =>
	  new()
	  {
		{ 1, ("cnxdashboard", "List of Subjects" , Icons.Material.Filled.Dashboard) },
		{ 2, ("idv", "IDV" , Icons.Material.Filled.Person) },
		{ 3, ("usermanagement", "User Management" , Icons.Material.Filled.ManageAccounts) },
	  	{ 4, ("chat", "Chat", Icons.Material.Filled.Chat) },
		{ 5,  ("cb2.0", "CB 2.0", Icons.Material.Filled.Score) },
		{ 6,  ("bulkprocessing", "Bulk Processing", Icons.Material.Filled.Dns) },
		{ 7,  ("ats", "ATS", Icons.Material.Filled.TrackChanges) },
		{ 8, ("logs", "Logs", Icons.Material.Filled.FileOpen) },
	  };
}
