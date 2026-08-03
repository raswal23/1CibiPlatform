namespace ATS.DTO;

public class DisputeOrderRequestDTO
{
	public Guid EmailInvitationId { get; set; }
	public string? SubjectName { get; set; }
	public string? Company { get; set; }
	public string? DisputeReason { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
}