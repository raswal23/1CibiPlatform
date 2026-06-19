namespace ATS.Data.Entities;

public class BulkUploadFileDetails
{
	public Guid FileID { get; set; }
	public Guid UploadedByUserId { get; set; }
	public string? FileName { get; set; }
	public string? FileKey { get; set; }
	public string? PackageType { get; set; }
	public string? OrderType { get; set; }
	public string? Status { get; set; }
	public DateTime DateCreated { get; set; }
}
