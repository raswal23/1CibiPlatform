namespace FrontendWebassembly.DTO.ATS;

public class PackageDetailsDTO
{
	public int PackageId { get; set; }
	public string PackageName { get; set; } = string.Empty;
	public string PackageDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int FollowUpEmail { get; set; }

	// Screening type: true = manual screening, false = data screening, null = not set.
	public bool? AutoChasing { get; set; }

	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
