namespace FrontendWebassembly.DTO.ATS;

public class EditPackageDTO
{
	public Guid PackageId { get; set; }
	public string? PackageName { get; set; }
	public bool IsActive { get; set; }
}
