namespace FrontendWebassembly.DTO.ATS;

public record DisputeOrderListDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? Requestor { get; set; }
	public string? TicketNumber { get; set; }
	public string? DisputeCategory { get; set; }
	public DateTime? DisputedAt { get; set; }
	public DateTime? OrderCompletedAt { get; set; }
}
