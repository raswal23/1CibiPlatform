namespace FrontendWebassembly.DTO.ATS;

public class AddPackageDTO
{
	public string PackageName { get; set; } = string.Empty;
	public string PackageDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int FollowUpEmail { get; set; }
}
