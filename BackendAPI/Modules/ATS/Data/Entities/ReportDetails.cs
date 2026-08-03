namespace ATS.Data.Entities;

public class ReportDetails
{
	public Guid ReportFileId { get; set; }
	public Guid EmailInvitationRequestId { get; set; }
	public string? HitStatus { get; set; }
	public string? ReportStatus { get; set; }
	public string? ReportFileName { get; set; }
	public string? ReportFileKey { get; set; }
	public DateTime ReportUploadedAt { get; set; }

	public EmailInvitationRequest? EmailInvitationRequest { get; set; }
}
