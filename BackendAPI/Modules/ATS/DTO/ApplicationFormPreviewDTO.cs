namespace ATS.DTO;

/// <summary>
/// Everything the applicant typed into the application form, for the read-only
/// preview dialog. File contents are not included - only what was entered.
/// Sections the applicant never reached are null.
/// </summary>
public record ApplicationFormPreviewDTO
{
	public string? SubjectName { get; set; }
	public string? FilledFormAt { get; set; }

	// True when the applicant went through the PhilSys liveness flow (a biometric
	// face capture was stored with the form).
	public bool PhilSysVerified { get; set; }

	public PersonalPreviewDTO? Personal { get; set; }
	public AddressPreviewDTO? Address { get; set; }
	public EducationPreviewDTO? Education { get; set; }
	public LicensePreviewDTO? License { get; set; }
	public List<EmployerPreviewDTO> Employers { get; set; } = [];
	public List<ReferencePreviewDTO> References { get; set; } = [];
	public SignaturePreviewDTO? Signature { get; set; }
}

public record PersonalPreviewDTO
{
	public string? PositionAppliedFor { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Suffix { get; set; }
	public string? Sex { get; set; }
	public string? DateOfBirth { get; set; }
	public string? MaritalStatus { get; set; }
	public string? Nationality { get; set; }
	public string? MobileNumber { get; set; }
	public string? TelephoneNumber { get; set; }
	public string? EmailAddress { get; set; }
	public string? EmailAlternative { get; set; }
	public string? SSS { get; set; }
	public string? TIN { get; set; }

	// Supporting documents uploaded on the verification step (names only, no content).
	public string? GovtIdFileName { get; set; }
	public string? NbiClearanceFileName { get; set; }
	public string? ResumeFileName { get; set; }
}

public record SignaturePreviewDTO
{
	public string? SignerName { get; set; }
	public string? SignatureDate { get; set; }
	public string? ConsentFormFileName { get; set; }
}

public record AddressPreviewDTO
{
	public string? CurrentAddress { get; set; }
	public string? CurrentCity { get; set; }
	public string? CurrentProvince { get; set; }
	public string? CurrentCountry { get; set; }
	public string? CurrentPostalCode { get; set; }
	public string? CurrentTypeOfOwnership { get; set; }
	public string? PermanentAddress { get; set; }
	public string? PermanentCity { get; set; }
	public string? PermanentProvince { get; set; }
	public string? PermanentCountry { get; set; }
	public string? PermanentPostalCode { get; set; }
}

public record EducationPreviewDTO
{
	public string? HighestEducationalAttainment { get; set; }
	public string? SchoolName { get; set; }
	public string? Degree { get; set; }
	public string? GraduationDate { get; set; }
	public string? DiplomaFileName { get; set; }
}

public record LicensePreviewDTO
{
	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public string? LicenseExpiryDate { get; set; }
	public string? LicenseFileName { get; set; }
}

public record EmployerPreviewDTO
{
	public string? CompanyName { get; set; }
	public string? JobTitle { get; set; }
	public string? CompanyAddress { get; set; }
	public string? StartDate { get; set; }
	public string? EndDate { get; set; }
	public string? CurrentlyEmployed { get; set; }
	public string? PermissionToContact { get; set; }
	public string? SupervisorName { get; set; }
	public string? SupervisorEmail { get; set; }
	public string? SupervisorContactNumber { get; set; }
	public string? CoeFileName { get; set; }
}

public record ReferencePreviewDTO
{
	public string? FullName { get; set; }
	public string? ProfessionalRelationship { get; set; }
	public string? AffiliatedCompany { get; set; }
	public string? Email { get; set; }
	public string? ContactNumber { get; set; }
	public string? ModeOfContact { get; set; }
	public string? BestTimeToContact { get; set; }
}
