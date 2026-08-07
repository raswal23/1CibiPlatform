namespace FrontendWebassembly.DTO.ATS;

public class ClientManagementViewModel
{
	public int ClientId { get; set; }
	public string ClientName { get; set; } = string.Empty;
	public string ClientDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public List<PackageDetailsDTO> Packages { get; set; } = new();
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
