namespace ATS.Data.Entities;

public class ArchiveReport
{
	public Guid ArchiveReportId { get; set; }
	public Guid EmailInvitationRequestId { get; set; }
	public string? ReportStatus { get; set; }
	public string? ReportFileName { get; set; }
	public string? ReportFileKey { get; set; }
	public DateTime ReportUploadedAt { get; set; }

	public EmailInvitationRequest? EmailInvitationRequest { get; set; }
}
