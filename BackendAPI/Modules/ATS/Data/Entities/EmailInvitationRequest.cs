namespace ATS.Data.Entities;

public class EmailInvitationRequest
{
	public Guid EmailInvitationID { get; set; }
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }
	public string? HashToken { get; set; }
	public string? ApplicationFormStatus { get; set; }
	public DateTime? FormCompletedAt { get; set; }
	public string? EmailSentStatus { get; set; }
	public DateTime? EmailSentAt { get; set; }
	public DateTime? HashTokenCreatedAt { get; set; }
	public DateTime? HashTokenExpiration { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
   public bool NeedsProjection { get; set; } = true;
	public DateTime? ProjectionUpdatedAt { get; set; }
	public bool IsDisputed { get; set; } = false;
	public DateTime? DisputedAt { get; set; }

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
   public ApplicantSearchProjection? ApplicantSearchProjection { get; set; }
}