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
	public DateTime? HashTokenCreated { get; set; }
	public DateTime? HashTokenExpiration { get; set; }

	// Navigation properties
	public PersonalDetails? PersonalDetails { get; set; }
	public AddressDetails? AddressDetails { get; set; }
	public EducationalBackground? EducationalBackground { get; set; }
	public LicensesDetails? LicensesDetails { get; set; }
	public ProfessionalExperiences? ProfessionalExperiences { get; set; }
	public ReferenceDetails? ReferenceDetails { get; set; }
	public ICollection<DocumentDetails>? Documents { get; set; }
}