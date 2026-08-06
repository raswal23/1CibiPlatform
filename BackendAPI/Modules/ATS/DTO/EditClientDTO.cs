namespace ATS.DTO;

public class EditClientDTO
{
	public Guid ClientId { get; set; }
	public string? ClientName { get; set; }
	public bool IsActive { get; set; }
}
