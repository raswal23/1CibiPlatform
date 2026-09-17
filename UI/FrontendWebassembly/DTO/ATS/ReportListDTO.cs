namespace FrontendWebassembly.DTO.ATS;

public record ReportListDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? SubjectName { get; set; }
	// The parts SubjectName is built from, so the edit dialog can prefill each
	// field without refetching the order.
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? LastName { get; set; }
	public string? Requestor { get; set; }
	public string? TicketNumber { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
	public string? SelectedPackage { get; set; }
	public string? RushNormal { get; set; }
	public string? HitStatus { get; set; }

	// Follow-up reminders still to come, as of today. Null means the question does not apply
	// (data screening, reminders switched off, or the form already answered or withdrawn),
	// which the column renders as a dash; 0 means the schedule is spent.
	public int? FollowUpEmailsRemaining { get; set; }

	public bool Selected { get; set; }
}
