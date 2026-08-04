namespace ATS.DTO;

public class ClientDetailsDTO
{
	public Guid ClientId { get; set; }
	public string? ClientName { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
