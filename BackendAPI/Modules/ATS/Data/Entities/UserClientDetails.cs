namespace ATS.Data.Entities;

public class UserClientDetails
{
	public Guid UserId { get; set; }
	public int ClientId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
