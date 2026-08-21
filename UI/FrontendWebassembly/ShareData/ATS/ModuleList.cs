namespace FrontendWebassembly.ShareData.ATS;

public static class ModuleList
{
	private static readonly int[] RestrictedAdministrationModuleIds = [6, 7, 8, 9, 11];

	public static Dictionary<int, (string path, string Name, string Icon)> List =>
		new()
		{
			{ 1, ("dashboard", "Dashboard", Icons.Material.Filled.Dashboard) },
			{ 2, ("neworder", "New Order", Icons.Material.Filled.AddCircle) },
			{ 3, ("searchreport", "Orders & Reports", Icons.Material.Filled.Assignment) },
			{ 4, ("disputeorder", "Disputes", Icons.Material.Filled.Warning) },
			{ 5, ("withdrawn", "Withdrawn", Icons.Material.Filled.Undo) },
			{ 6, ("packagemanagement", "Package Management", Icons.Material.Filled.Inventory2) },
			{ 7, ("clientmanagement", "Client Management", Icons.Material.Filled.Business) },
			{ 8, ("rolemanagement", "Role Management", Icons.Material.Filled.Group) },
			{ 9, ("modulemanagement", "Module Management", Icons.Material.Filled.Apps) },
			{ 10, ("usermanagement", "User Management", Icons.Material.Filled.ManageAccounts) },
			{ 11, ("clientassigning", "Client Assigning", Icons.Material.Filled.AssignmentInd) },
			{ 12, ("aiassistant", "AI Assistant", Icons.Material.Filled.SmartToy) },
			{ 13, ("bulkuploads", "Bulk Uploads", Icons.Material.Filled.CloudUpload) }
		};

	// Modules that belong in the primary sidebar navigation rather than under Manage.
	public static bool IsPrimaryNavigationModule(int moduleId) =>
		moduleId <= 5 || moduleId == 12 || moduleId == 13;

	public static bool IsVisibleForAdministration(int moduleId, bool canViewAllModules) =>
		canViewAllModules || !RestrictedAdministrationModuleIds.Contains(moduleId);
}
