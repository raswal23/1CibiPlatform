namespace FrontendWebassembly.DTO.ATS;

public record EditSubjectNameDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? LastName { get; set; }
}

public record SubjectNameDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? LastName { get; set; }
	public string? SubjectName { get; set; }
}

public record EditSubjectNameResponseDTO
{
	public SubjectNameDTO? Subject { get; set; }
}
