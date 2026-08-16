namespace FrontendWebassembly.DTO.ATS;

public record AtsOrderSummaryDTO
{
	public Guid EmailInvitationRequestId { get; set; }
	public string? SubjectName { get; set; }
	public string? OrderStatus { get; set; }
	public string? SelectedPackage { get; set; }
	public string? Requestor { get; set; }
	public string? HitStatus { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
}
