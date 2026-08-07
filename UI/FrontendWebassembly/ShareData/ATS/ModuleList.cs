namespace FrontendWebassembly.ShareData.ATS;

public static class ModuleList
{
	public static Dictionary<int, (string path, string Name, string Icon)> List =>
		new()
		{
			{ 1, ("dashboard", "Dashboard", Icons.Material.Filled.Dashboard) },
			{ 2, ("neworder", "New Order", Icons.Material.Filled.AddCircle) },
			{ 3, ("searchreport", "Orders & Reports", Icons.Material.Filled.Assignment) },
			{ 4, ("disputeorder", "Disputes", Icons.Material.Filled.Warning) },
			{ 5, ("withdrawn", "Withdrawn", Icons.Material.Filled.Undo) },
			{ 6, ("settings/packagemanagement", "Package Management", Icons.Material.Filled.Inventory2) },
			{ 7, ("settings/clientmanagement", "Client Management", Icons.Material.Filled.Business) },
			{ 8, ("rolemanagement", "Role Management", Icons.Material.Filled.Group) },
			{ 9, ("modulemanagement", "Module Management", Icons.Material.Filled.Apps) },
			{ 10, ("settings/usermanagement", "User Management", Icons.Material.Filled.ManageAccounts) }
		};
}
