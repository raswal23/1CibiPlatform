namespace ATS.Data.Entities;

public class OrderStatusHistory
{
	public Guid OrderStatusHistoryId { get; set; }
	public Guid EmailInvitationRequestId { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string? PreviousStatus { get; set; }
	public string NewStatus { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public DateTime OccurredAt { get; set; }
	public Guid? ChangedByUserId { get; set; }
	public EmailInvitationRequest EmailInvitationRequest { get; set; } = null!;
}
