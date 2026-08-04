namespace ATS.Data.Entities;

public class ClientDetails
{
	public Guid ClientId { get; set; }
	public string? ClientName { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
