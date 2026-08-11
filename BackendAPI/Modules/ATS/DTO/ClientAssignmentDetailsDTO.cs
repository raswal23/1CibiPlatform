namespace ATS.DTO;

public class ClientAssignmentDetailsDTO
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;
	public int? ClientId { get; set; }
	public string? ClientName { get; set; }
	public DateTime? AssignedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
}
