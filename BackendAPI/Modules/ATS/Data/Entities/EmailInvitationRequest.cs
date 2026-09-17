namespace ATS.Data.Entities;

public class EmailInvitationRequest
{
	public Guid EmailInvitationID { get; set; }
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
	public string? Requestor { get; set; }
	// The package this order was placed under. PackageId is the relationship;
	// SelectPackage is a denormalised label kept for reads, search and exports, and
	// refreshed if the package is ever renamed.
	public int PackageId { get; set; }
	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }
	public string? HashToken { get; set; }
	public int? ClientId { get; set; }
	public Guid? RequestorId { get; set; }

	// Screening type snapshotted from the package at order time (true = manual,
	// false = data, null = unknown/legacy). Stored on the order because the
	// package's own classification can be edited later.
	public bool? AutoChasing { get; set; }

	// Candidate identity captured at order entry. Required for data-screening web
	// orders (no application form is sent, so the candidate cannot supply them);
	// null for manual orders, bulk rows and public API orders.
	public DateOnly? DateOfBirth { get; set; }
	public string? SSSNumber { get; set; }
	public string? TINNumber { get; set; }

	public string? ApplicationFormStatus { get; set; }
	public DateTime? FormCompletedAt { get; set; }
	public string? EmailSentStatus { get; set; }
	public DateTime? EmailSentAt { get; set; }
	public DateTime? EmailClaimedAt { get; set; }
	public int EmailSendAttempts { get; set; }
	// Null means the invitation came from a single inquiry rather than a bulk upload.
	public Guid? BulkFileID { get; set; }
	public DateTime? HashTokenCreatedAt { get; set; }
	// Retained for history only. The application form link no longer expires, so
	// nothing reads or writes this after the expiry removal; rows created before it
	// keep the value they were stamped with.
	public DateTime? HashTokenExpiration { get; set; }
	// The Manila calendar date of the last reminder queued for this order. Two jobs at once:
	//
	//   1. The once-per-day guarantee. The chaser runs hourly, so a due row would otherwise be
	//      released on all 24 passes; "<> today" collapses those to the first pass of the day.
	//   2. Paired with a null EmailSentAt, it tells the sender to use reminder copy rather
	//      than first-invitation copy.
	//
	// DateOnly rather than a timestamp because the rule is calendar-shaped - one per day, not
	// one per 24 hours. Compared in Asia/Manila; see ReleaseDueFollowUpInvitationsAsync.
	public DateOnly? LastFollowUpSentDate { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
	public bool NeedsProjection { get; set; } = true;
	public DateTime? ProjectionUpdatedAt { get; set; }
	public string? DisputeCategory { get; set; }
	public DateTime? DisputedAt { get; set; }

	// OMS auto-ticketing. The order is queued at enrolment and the background job
	// claims it by writing TicketStatus, exactly as the email queue does above.
	// IsTicketed is the terminal flag: false means still claimable, true means a
	// ticket number came back from OMS and the row must never be picked up again.
	public string? TicketStatus { get; set; }
	public bool IsTicketed { get; set; }
	public string? TicketNumber { get; set; }
	public DateTime? TicketDeliveryDate { get; set; }
	public DateTime? TicketClaimedAt { get; set; }
	public int TicketAttempts { get; set; }
	public string? TicketError { get; set; }

	// Navigation properties
	public PersonalDetails? PersonalDetails { get; set; }
	public AddressDetails? AddressDetails { get; set; }
	public EducationalBackground? EducationalBackground { get; set; }
	public LicensesDetails? LicensesDetails { get; set; }
	public ProfessionalExperiences? ProfessionalExperiences { get; set; }
	public ReferenceDetails? ReferenceDetails { get; set; }
	public SignatureDetails? SignatureDetails { get; set; }
	public ICollection<DocumentDetails>? Documents { get; set; }
	public ICollection<ReportDetails>? ReportDetails { get; set; }
	public ICollection<ArchiveReport>? ArchiveReports { get; set; }
	public ICollection<OrderStatusHistory>? OrderStatusHistories { get; set; }
	public ApplicantSearchProjection? ApplicantSearchProjection { get; set; }
}
