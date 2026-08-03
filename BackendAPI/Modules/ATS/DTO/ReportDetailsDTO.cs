namespace ATS.Data.DTO;

public record ReportDetailsDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? HitStatus { get; set; }
	public string? ReportStatus { get; set; }
	public IFormFile? ReportFile { get; set; }
}
