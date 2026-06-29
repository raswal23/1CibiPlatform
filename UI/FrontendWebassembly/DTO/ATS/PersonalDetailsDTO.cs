namespace FrontendWebassembly.DTO.ATS;

public record PersonalDetailsDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? PositionAppliedFor { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Suffix { get; set; }
	public string? Sex { get; set; }
	public DateOnly? DOB { get; set; }
	public string? MobileNumber { get; set; }
	public string? EmailAlternative { get; set; }
	public byte[]? AdditionalGovtIDFile { get; set; }
	public string? AdditionalGovtIDFileName { get; set; }
	public byte[]? NBIClearanceFile { get; set; }
	public string? NBIClearanceFileName { get; set; }
	public byte[]? ResumeFile { get; set; }
	public string? ResumeFileName { get; set; }
}
