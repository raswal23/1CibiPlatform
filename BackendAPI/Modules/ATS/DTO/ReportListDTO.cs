namespace ATS.DTO;

public record ReportListDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? SubjectName { get; set; }
	// The name parts SubjectName is built from, so the edit dialog can prefill
	// each field without refetching the order.
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

	// How many follow-up reminders this order will still receive, as of today.
	//
	// Null means the question does not apply - a data-screening order, a package with
	// reminders switched off, or a form that has already been answered or withdrawn. That is
	// deliberately distinct from 0, which means "chasing applies, but the schedule is spent".
	// The UI shows a dash for null and "Done" for 0.
	public int? FollowUpEmailsRemaining { get; set; }
}
