namespace ATS.Data.Entities;

public class BulkUploadFileDetails
{
	public Guid FileID { get; set; }
	// The uploader's identity is captured here at upload time because the background job
	// that parses the file has no HTTP context to resolve it from.
	public Guid? UploadedByUserId { get; set; }
	public string? Requestor { get; set; }
	public int? ClientId { get; set; }
	public string? FileName { get; set; }
	public string? FileKey { get; set; }
	public string? PackageType { get; set; }
	public string? OrderType { get; set; }
	public string? Status { get; set; }
	public DateTime? ClaimedAt { get; set; }
	public DateTime DateCreated { get; set; }
}
