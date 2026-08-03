namespace ATS.Data.Entities;

public class ApplicantSearchProjection
{
	public Guid EmailInvitationRequestId { get; set; }

	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
	public string? ApplicationFormStatus { get; set; }

	public string? PositionAppliedFor { get; set; }
	public string? MaritalStatus { get; set; }
	public string? Nationality { get; set; }
	public string? Sex { get; set; }
	public DateOnly? DOB { get; set; }
	public string? SSS { get; set; }
	public string? TIN { get; set; }
	public string? EmailAlternative { get; set; }

	public string? CurrentAddress { get; set; }
	public string? CurrentCity { get; set; }
	public string? CurrentProvince { get; set; }
	public string? CurrentCountry { get; set; }
	public string? CurrentPostalCode { get; set; }
	public string? PermanentAddress { get; set; }
	public string? PermanentCity { get; set; }
	public string? PermanentProvince { get; set; }
	public string? PermanentCountry { get; set; }
	public string? PermanentPostalCode { get; set; }

	public string? HighestEducationalAttainment { get; set; }
	public string? BachelorsSchoolName { get; set; }
	public string? BachelorsDegree { get; set; }
	public string? MastersSchoolName { get; set; }
	public string? MastersDegree { get; set; }
	public string? PhDSchoolName { get; set; }
	public string? DoctorateDegree { get; set; }

	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public DateOnly? LicenseExpiryDate { get; set; }

	public string? Emp1CompanyName { get; set; }
	public string? Emp1JobTitle { get; set; }
	public string? Emp2CompanyName { get; set; }
	public string? Emp2JobTitle { get; set; }
	public string? Emp3CompanyName { get; set; }
	public string? Emp3JobTitle { get; set; }

	public string? Ref1FullName { get; set; }
	public string? Ref1ContactNumber { get; set; }
	public string? Ref2FullName { get; set; }
	public string? Ref2ContactNumber { get; set; }
	public string? Ref3FullName { get; set; }
	public string? Ref3ContactNumber { get; set; }

	public string? SignerName { get; set; }
	public DateOnly? SignatureDate { get; set; }

	public DateTime? ProjectionUpdatedAt { get; set; }

	public EmailInvitationRequest? EmailInvitationRequest { get; set; }
}
