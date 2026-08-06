namespace FrontendWebassembly.DTO.ATS;

public class ClientDetailsDTO
{
	public Guid ClientId { get; set; }
	public string ClientName { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
