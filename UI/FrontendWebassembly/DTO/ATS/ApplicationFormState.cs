namespace FrontendWebassembly.DTO.ATS;

public sealed class ApplicationFormState
{
	public int Version { get; set; } = 1;
	public Guid EmailInvitationId { get; set; }
	public DateTime? LastModifiedAtUtc { get; set; }
	public PersonalDetailsState PersonalDetails { get; set; } = new();
	public AddressDetailsState AddressDetails { get; set; } = new();
	public EducationalBackgroundState EducationalBackground { get; set; } = new();
	public LicensesDetailsState LicensesDetails { get; set; } = new();
	public ProfessionalExperiencesState ProfessionalExperiences { get; set; } = new();
	public ReferenceDetailsState ReferenceDetails { get; set; } = new();
	public SignatureDetailsState SignatureDetails { get; set; } = new();
}

public sealed class PersonalDetailsState
{
	public string? PositionAppliedFor { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Suffix { get; set; }
	public string? Sex { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public string? MobileNumber { get; set; }
	public string? EmailAlternative { get; set; }
	public bool NoMiddleName { get; set; }
}

public sealed class AddressDetailsState
{
	public string? CurrentAddress { get; set; }
	public string? CurrentCity { get; set; }
	public string? CurrentProvince { get; set; }
	public string? CurrentCountry { get; set; }
	public string? CurrentPostalCode { get; set; }
	public string? TypeOfOwnership { get; set; }
	public string? OwnershipOtherText { get; set; }
	public string? PermanentAddress { get; set; }
	public string? PermanentCity { get; set; }
	public string? PermanentProvince { get; set; }
	public string? PermanentCountry { get; set; }
	public string? PermanentPostalCode { get; set; }
	public bool SameAsPermanent { get; set; }
}

public sealed class EducationalBackgroundState
{
	public string? HighestEducationalAttainment { get; set; }
	public DateTime? GraduationDate { get; set; }
	public string? DegreeWithMajor { get; set; }
	public string? AcademicInstitution { get; set; }
}

public sealed class LicensesDetailsState
{
	public bool HasProfessionalLicense { get; set; }
	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public DateTime? LicenseExpiryDate { get; set; }
}

public sealed class ProfessionalExperiencesState
{
	public bool AddEmployer2 { get; set; }
	public bool AddEmployer3 { get; set; }
	public EmployerState Employer1 { get; set; } = new();
	public EmployerState Employer2 { get; set; } = new();
	public EmployerState Employer3 { get; set; } = new();
}

public sealed class EmployerState
{
	public string? CompanyName { get; set; }
	public bool CurrentlyEmployed { get; set; }
	public bool PermissionToContact { get; set; }
	public string? CompanyCity { get; set; }
	public string? CompanyProvince { get; set; }
	public string? CompanyCountry { get; set; }
	public string? CompanyPostalCode { get; set; }
	public DateTime? DatePermittedToContact { get; set; }
	public string? JobTitle { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public string? SupervisorName { get; set; }
	public string? SupervisorContactNumber { get; set; }
}

public sealed class ReferenceDetailsState
{
	public bool AddReference3 { get; set; }
	public ReferenceState Reference1 { get; set; } = new();
	public ReferenceState Reference2 { get; set; } = new();
	public ReferenceState Reference3 { get; set; } = new();
}

public sealed class ReferenceState
{
	public string? FullName { get; set; }
	public string? ProfessionalRelationship { get; set; }
	public string? AffiliatedCompany { get; set; }
	public string? Email { get; set; }
	public string? ContactNumber { get; set; }
	public string? ModeOfContact { get; set; }
	public DateTime? BestDate { get; set; }
	public TimeSpan? BestTime { get; set; }
}

public sealed class SignatureDetailsState
{
	public bool Consent { get; set; }
	public bool DeclineConsent { get; set; }
	public string? SignerName { get; set; }
	public DateTime? SignatureDate { get; set; }
}
