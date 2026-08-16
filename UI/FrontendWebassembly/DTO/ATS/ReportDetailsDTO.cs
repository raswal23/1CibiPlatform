namespace FrontendWebassembly.DTO.ATS;

public record ReportDetailsDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? HitStatus { get; set; }
	public string? ReportStatus { get; set; }
	public IBrowserFile? ReportFile { get; set; }
}
