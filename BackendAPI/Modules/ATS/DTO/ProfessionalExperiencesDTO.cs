namespace ATS.DTO;

public record ProfessionalExperiencesDTO
{
	public Guid ProfessionalExperiencesID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? Emp1CompanyName { get; set; }
	public bool? Emp1CurrentlyEmployed { get; set; }
	public bool? Emp1PermissionToContact { get; set; }
	public string? Emp1CompanyCity { get; set; }
	public string? Emp1CompanyProvince { get; set; }
	public string? Emp1CompanyCountry { get; set; }
	public string? Emp1CompanyPostalCode { get; set; }
	public DateTime? Emp1StartDate { get; set; }
	public DateTime? Emp1EndDate { get; set; }
	public string? Emp1JobTitle { get; set; }
	public string? Emp1SupervisorName { get; set; }
	public string? Emp1SupervisorContactNumber { get; set; }
	public string? Emp2CompanyName { get; set; }
	public bool? Emp2CurrentlyEmployed { get; set; }
	public bool? Emp2PermissionToContact { get; set; }
	public string? Emp2CompanyCity { get; set; }
	public string? Emp2CompanyProvince { get; set; }
	public string? Emp2CompanyCountry { get; set; }
	public string? Emp2CompanyPostalCode { get; set; }
	public DateTime? Emp2StartDate { get; set; }
	public DateTime? Emp2EndDate { get; set; }
	public string? Emp2JobTitle { get; set; }
	public string? Emp2SupervisorName { get; set; }
	public string? Emp2SupervisorContactNumber { get; set; }
	public string? Emp3CompanyName { get; set; }
	public bool? Emp3CurrentlyEmployed { get; set; }
	public bool? Emp3PermissionToContact { get; set; }
	public string? Emp3CompanyCity { get; set; }
	public string? Emp3CompanyProvince { get; set; }
	public string? Emp3CompanyCountry { get; set; }
	public string? Emp3CompanyPostalCode { get; set; }
	public DateTime? Emp3StartDate { get; set; }
	public DateTime? Emp3EndDate { get; set; }
	public string? Emp3JobTitle { get; set; }
	public string? Emp3SupervisorName { get; set; }
	public string? Emp3SupervisorContactNumber { get; set; }
	public byte[]? COEUploadFile { get; set; }
	public string? COEUploadFileName { get; set; }

	public DateTime? CreatedDate { get; set; }
}
