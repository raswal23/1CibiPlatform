namespace ATS.DTO;

public class UserClientDetailsDTO
{
	public Guid UserId { get; set; }
	public int ClientId { get; set; }
	public string? ClientName { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
