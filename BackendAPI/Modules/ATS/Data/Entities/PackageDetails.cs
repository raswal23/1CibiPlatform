namespace ATS.Data.Entities;

public class PackageDetails
{
	public int PackageId { get; set; }
	public string? PackageName { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
