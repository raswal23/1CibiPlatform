namespace FrontendWebassembly.DTO.ATS;

public class PackageDetailsDTO
{
	public Guid PackageId { get; set; }
	public string? PackageName { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
