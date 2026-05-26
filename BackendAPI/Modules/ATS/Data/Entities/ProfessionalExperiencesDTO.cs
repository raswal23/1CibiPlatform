namespace ATS.Data.Entities;

public record ProfessionalExperiencesDTO
{
	public Guid ProfessionalExperiencesID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? Emp1CompanyName { get; set; }
	public string? Emp1CurrentlyEmployed { get; set; }
	public string? Emp1PermissionToContact { get; set; }
	public string? Emp1CompanyAddress { get; set; }
	public string? Emp1StartDate { get; set; }
	public string? Emp1EndDate { get; set; }
	public string? Emp1JobTitle { get; set; }
	public string? Emp1ReasonForLeaving { get; set; }
	public string? Emp1SupervisorName { get; set; }
	public string? Emp1SupervisorContactNumber { get; set; }
	public string? Emp1SupervisorEmail { get; set; }
	public string? Emp2CompanyName { get; set; }
	public string? Emp2CurrentlyEmployed { get; set; }
	public string? Emp2PermissionToContact { get; set; }
	public string? Emp2CompanyAddress { get; set; }
	public string? Emp2StartDate { get; set; }
	public string? Emp2EndDate { get; set; }
	public string? Emp2JobTitle { get; set; }
	public string? Emp2ReasonForLeaving { get; set; }
	public string? Emp2SupervisorName { get; set; }
	public string? Emp2SupervisorContactNumber { get; set; }
	public string? Emp2SupervisorEmail { get; set; }
	public string? Emp3CompanyName { get; set; }
	public string? Emp3CurrentlyEmployed { get; set; }
	public string? Emp3PermissionToContact { get; set; }
	public string? Emp3CompanyAddress { get; set; }
	public string? Emp3StartDate { get; set; }
	public string? Emp3EndDate { get; set; }
	public string? Emp3JobTitle { get; set; }
	public string? Emp3ReasonForLeaving { get; set; }
	public string? Emp3SupervisorName { get; set; }
	public string? Emp3SupervisorContactNumber { get; set; }
	public string? Emp3SupervisorEmail { get; set; }
	public byte[]? COEUpload { get; set; }
	public DateTime? CreatedDate { get; set; }
}
