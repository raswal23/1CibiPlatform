namespace FrontendWebassembly.DTO.ATS;

public class EditClientDTO
{
	public int ClientId { get; set; }
	public string ClientName { get; set; } = string.Empty;
	public string ClientDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public HashSet<int> PackageIds { get; set; } = new();
}
