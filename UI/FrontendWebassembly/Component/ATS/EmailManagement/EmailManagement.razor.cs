namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// Host for the two email administration screens: <c>EmailAccountManagement</c>, the mailboxes
/// notices are sent <em>from</em>, and <c>EmailProcessManagement</c>, the mailboxes notices are
/// copied <em>to</em>. Both are module 16.
/// </summary>
/// <remarks>
/// Owns no data and calls no service. Each tab is the existing screen rendered as a child, which is
/// what keeps this a UI-level grouping: neither screen's loading, validation or service calls
/// changed, and both still carry their own <c>RequireATSModule(16)</c> as a second check.
/// </remarks>
public partial class EmailManagement
{
	private string? ActiveTab { get; set; }

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		// Without this guard the RequirePermission/RequireATSModule attributes are inert.
		if (!IsPageAuthorized)
		{
			return;
		}

		// Accounts first because it is the tab the "invitations have stopped" notification points
		// at: that outage is about sender capacity, and whoever follows the link should land on the
		// screen that explains it rather than one tab over.
		ActiveTab = "Accounts";
	}

	private string GetAccountsSegmentClass() =>
		ActiveTab == "Accounts" ? "ats-segment-btn active" : "ats-segment-btn";

	private string GetReceiverSegmentClass() =>
		ActiveTab == "Receiver" ? "ats-segment-btn active" : "ats-segment-btn";

	private void SetActiveTab(string tab) => ActiveTab = tab;
}
