namespace ATS.DTO;

// Intermediate projection for the reports lists. Pages are fetched in this
// shape so the service retains the order-created key used for keyset paging
// before mapping into the public ReportListDTO.
public sealed class ReportRowDTO
{
	public Guid EmailInvitationID { get; init; }
	public string? FirstName { get; init; }
	public string? MiddleInitial { get; init; }
	public string? LastName { get; init; }
	public string? Requestor { get; init; }
	public string? TicketNumber { get; init; }
	public string? OrderStatus { get; init; }
	public DateTime? OrderCreatedAt { get; init; }
	public DateTime? OrderCompletedAt { get; init; }
	public string? SelectPackage { get; init; }
	public string? RushNormal { get; init; }
	public string? HitStatus { get; init; }

	// The inputs the remaining-reminder count is derived from, carried raw rather than
	// pre-computed. "How many are left" depends on today's date, so a number calculated in
	// the database would be wrong the moment the row is cached - and these rows are cached.
	// See ReportService, which does the arithmetic per request.
	public int PackageFollowUpEmail { get; init; }
	public bool ChasesCandidate { get; init; }
	public string? ApplicationFormStatus { get; init; }
}
