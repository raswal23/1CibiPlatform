namespace FrontendWebassembly.DTO.ATS;

public class ClientDetailsDTO
{
	public int ClientId { get; set; }
	public string ClientName { get; set; } = string.Empty;
	public string ClientDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int PackageId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
