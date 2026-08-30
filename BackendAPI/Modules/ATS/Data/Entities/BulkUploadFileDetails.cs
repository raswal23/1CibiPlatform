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

	// How the file reached us - the web console or the public API. Captured here for the
	// same reason as the uploader above: the parsing job has no HTTP context, and it is
	// the job that writes each row's order-history entry.
	public string? Source { get; set; }

	// Per-row outcome of the last parse. The file is parsed asynchronously, long after
	// the upload response has returned, so rejected rows are recorded here for the
	// uploader to read back rather than being returned inline.
	public int AcceptedRowCount { get; set; }
	public int RejectedRowCount { get; set; }
	public string? RejectedRows { get; set; }
}
