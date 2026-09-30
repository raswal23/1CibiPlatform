namespace ATS.Constants;

/// <summary>
/// ATS module ids. These must stay in sync with
/// <c>UI/FrontendWebassembly/ShareData/ATS/ModuleList.cs</c>.
/// </summary>
public static class AtsModuleIds
{
	public const int Dashboard = 1;
	public const int NewOrder = 2;
	public const int OrdersAndReports = 3;
	public const int Disputes = 4;
	public const int Withdrawn = 5;
	public const int PackageManagement = 6;
	public const int ClientManagement = 7;
	public const int RoleManagement = 8;
	public const int ModuleManagement = 9;
	public const int UserManagement = 10;
	public const int ClientAssigning = 11;
	public const int AIAssistant = 12;
	public const int BulkUploads = 13;
	public const int TicketingStatus = 14;
	public const int AuditTrail = 15;

	/// <summary>
	/// Both halves of email administration: the sender accounts notices go out through, and the
	/// copy lists deciding who else is Cc'd on each notice. One id because the console presents
	/// them as two tabs of one screen.
	/// </summary>
	/// <remarks>
	/// Was <c>EmailAccountManagement</c> before the copy-list screen was grouped with it. Renaming
	/// the constant is safe - the id is what is stored, in <c>ats."ModuleDetails"</c> and in one
	/// <c>ats."UserDetails"</c> row per grant, and 16 is unchanged.
	/// </remarks>
	public const int EmailManagement = 16;

	// 17 is deliberately absent and must NOT be reused. It was EmailProcessManagement for one
	// uncommitted iteration of the copy-list screen, before that screen was folded into 16. Any
	// database that booted in the meantime - a developer machine, above all - has an orphan
	// ModuleDetails row for 17 that the runtime seeder will not remove, because it only inserts
	// ids the catalogue is missing. Handing 17 to a different feature would silently grant it to
	// whoever was granted the old one. Delete the orphan row and its UserDetails grants before
	// giving the id another meaning; see
	// docs/features/ats-email-process/ats-email-process_code_explanation.md.
}
