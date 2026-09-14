namespace FrontendWebassembly.ShareData.ATS;

public static class ModuleList
{
	// 15 (Audit Trail) is restricted for a different reason than the rest: a trail the
	// audited user can read is a weaker control, so only a platform super admin sees it.
	// The backend enforces the same rule independently.
	//
	// 16 (Email Accounts) is restricted because the accounts it manages are the credentials
	// every outbound invitation is sent through: deleting one silently shifts that volume onto
	// the remaining senders, and a wrong daily limit stalls the queue. Same rule as 15 - the
	// backend enforces it independently.
	private static readonly int[] RestrictedAdministrationModuleIds = [6, 7, 8, 9, 11, 15, 16];

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
			{ 13, ("bulkuploads", "Bulk Uploads Status", Icons.Material.Filled.CloudUpload) },
			{ 14, ("ticketingstatus", "Ticketing Status", Icons.Material.Filled.ConfirmationNumber) },
			{ 15, ("audittrail", "Audit Trail", Icons.Material.Filled.History) },
			{ 16, ("emailaccounts", "Email Accounts", Icons.Material.Filled.AlternateEmail) }

			// Notifications (/s&i/ats/notifications) is deliberately NOT here. This list
			// drives both the sidebar and ATSLayout.CanAccessRoute, and every id in it must
			// exist in the backend module seed data and be grantable. Notifications is not a
			// permissioned module - anyone with ATS access has an inbox - so adding it would
			// show a link only super admins could follow and bounce everyone else to
			// /access-denied. The page is reached from the bell instead.
		};

	// Modules that belong in the primary sidebar navigation rather than under Manage.
	public static bool IsPrimaryNavigationModule(int moduleId) =>
		moduleId <= 5 || moduleId == 12 || moduleId == 13 || moduleId == 14;

	public static bool IsVisibleForAdministration(int moduleId, bool canViewAllModules) =>
		canViewAllModules || !RestrictedAdministrationModuleIds.Contains(moduleId);
}
