namespace ATS.DTO;

public record ReportListDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? SubjectName { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
	public string? SelectedPackage { get; set; }
	public string? HitStatus { get; set; }
}
