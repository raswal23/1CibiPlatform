using FrontendWebassembly.ShareData.ATS;

namespace FrontendWebassembly.Component.ATS.Notifications;

public partial class NotificationItem
{
	[Parameter, EditorRequired]
	public NotificationDTO Item { get; set; } = default!;

	[Parameter]
	public EventCallback<NotificationDTO> OnActivate { get; set; }

	private string RowCssClass =>
		Item.IsRead ? "ats-notif-row" : "ats-notif-row is-unread";

	// The accent travels with the type, so the icon tile and the row share one lookup.
	private string IconCssClass => $"ats-notif-icon {AccentModifier}";

	private string AriaLabel =>
		Item.IsRead ? Item.Title : $"Unread: {Item.Title}";

	private string TypeIcon => Item.Type switch
	{
		AtsNotificationTypes.ApplicationFormSubmitted => Icons.Material.Filled.AssignmentTurnedIn,
		AtsNotificationTypes.BulkUploadCompleted => Icons.Material.Filled.CloudDone,
		AtsNotificationTypes.BulkEmailsCompleted => Icons.Material.Filled.MarkEmailRead,
		AtsNotificationTypes.OrderCompleted => Icons.Material.Filled.CheckCircle,
		AtsNotificationTypes.ReportReady => Icons.Material.Filled.Description,
		AtsNotificationTypes.OrderDisputed => Icons.Material.Filled.Gavel,
		AtsNotificationTypes.TicketingFailed => Icons.Material.Filled.ErrorOutline,
		AtsNotificationTypes.InvitationEmailFailed => Icons.Material.Filled.MarkEmailUnread,
		AtsNotificationTypes.EmailAccountsExhausted => Icons.Material.Filled.Unsubscribe,
		AtsNotificationTypes.EmailAccountNeedsReverification => Icons.Material.Filled.KeyOff,

		// An unknown type is a server that knows about something this build does not.
		// Render it neutrally rather than dropping it.
		_ => Icons.Material.Filled.Notifications
	};

	private string AccentModifier => Item.Type switch
	{
		AtsNotificationTypes.ApplicationFormSubmitted => "is-info",
		AtsNotificationTypes.BulkUploadCompleted => "is-info",
		AtsNotificationTypes.BulkEmailsCompleted => "is-success",
		AtsNotificationTypes.OrderCompleted => "is-success",
		AtsNotificationTypes.ReportReady => "is-success",
		AtsNotificationTypes.OrderDisputed => "is-warn",
		AtsNotificationTypes.TicketingFailed => "is-danger",
		AtsNotificationTypes.InvitationEmailFailed => "is-danger",

		// Warn, not danger: the queue is paused with its rows intact and a daily cap clears
		// itself. Overstating it as a failure trains people to ignore the red ones.
		AtsNotificationTypes.EmailAccountsExhausted => "is-warn",

		// Danger, because nothing clears this one except someone re-entering the password.
		AtsNotificationTypes.EmailAccountNeedsReverification => "is-danger",
		_ => "is-neutral"
	};

	// "3m ago" rather than a timestamp: in a notification list the age is what matters,
	// and an absolute time forces the reader to do the subtraction. Falls back to a date
	// once the difference stops being useful.
	private string RelativeTime
	{
		get
		{
			var elapsed = DateTime.UtcNow - Item.CreatedAt;

			if (elapsed < TimeSpan.Zero)
			{
				// Clock skew between the server and the browser. Reads better than
				// "-2s ago".
				return "just now";
			}

			return elapsed switch
			{
				{ TotalSeconds: < 60 } => "just now",
				{ TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m ago",
				{ TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h ago",
				{ TotalDays: < 7 } => $"{(int)elapsed.TotalDays}d ago",
				_ => Item.CreatedAt.ToLocalTime().ToString("MMM d, yyyy")
			};
		}
	}
}
