namespace FrontendWebassembly.DTO.ATS;

public record DisputeOrderListDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? DisputeCategory { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
    public DateTime? OrderCompletedAt { get; set; }
}
