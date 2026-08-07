namespace ATS.Data.Entities;

public class PackageDetails
{
	public int PackageId { get; set; }
	public string PackageName { get; set; } = string.Empty;
	public string PackageDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int FollowUpEmail { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
