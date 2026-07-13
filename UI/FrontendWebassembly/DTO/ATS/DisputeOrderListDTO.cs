namespace FrontendWebassembly.DTO.ATS;

public record DisputeOrderListDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? OrderStatus { get; set; }
	public DateTime? OrderCreatedAt { get; set; }
	public DateTime? OrdeCompletedAt { get; set; }
	public bool IsDiputed { get; set; }
}
