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
		{ 8,  ("oms", "OMS", Icons.Material.Filled.Inventory2) },
		{ 9,  ("pi", "PI", Icons.Material.Filled.PersonSearch) },
		{ 10, ("bi", "BI", Icons.Material.Filled.Insights) },
		{ 11, ("bcs", "BCS", Icons.Material.Filled.SupportAgent) },
		{ 12, ("general", "General", Icons.Material.Filled.Tune) },
		{ 13, ("securitycontrol", "Security Control", Icons.Material.Filled.Security) },
		{ 14, ("customization", "Customization", Icons.Material.Filled.Palette) },
		{ 15, ("automation", "Automation", Icons.Material.Filled.AutoMode) },
		{ 16, ("dataadministration", "Data Administration", Icons.Material.Filled.Storage) }
	  };
}
