namespace FrontendWebassembly.DTO.ATS;

public class AddClientDTO
{
	public string ClientName { get; set; } = string.Empty;
	public string ClientDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public HashSet<int> PackageIds { get; set; } = new();
}
