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
	public string? ApplicationFormStatus { get; set; }
	public DateTime? FormCompletedAt { get; set; }
	public string? EmailSentStatus { get; set; }
	public DateTime? EmailSentAt { get; set; }
	public DateTime? EmailClaimedAt { get; set; }
	public int EmailSendAttempts { get; set; }
	// Null means the invitation came from a single inquiry rather than a bulk upload.
	public Guid? BulkFileID { get; set; }
	public DateTime? HashTokenCreatedAt { get; set; }
	public DateTime? HashTokenExpiration { get; set; }
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
