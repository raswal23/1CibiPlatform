namespace ATS.Data.DTO;

public record BulkUploadFileDetailsDTO
{
	// Set by InsertBulkSubjectAsync once the file row is created, so an API caller can
	// poll the file it just uploaded. The web console ignores it.
	public Guid FileId { get; set; }

	public Guid UploadedByUserId { get; set; }
	public string? FileName { get; set; }
	public string? Status { get; set; }
	// Resolved from PackageType by the order validator, not supplied by the caller.
	public int PackageId { get; set; }

	public string? PackageType { get; set; }
	public string? OrderType { get; set; }

	// Screening type chosen in the web console (true = manual, false = data), used
	// to cross-check the selected package. Null for public API callers, which skip
	// the check. Before persisting, the service overwrites it with the package's
	// own classification, which is snapshotted onto the file.
	public bool? AutoChasing { get; set; }

	public DateTime DateCreated { get; set; }
	public IFormFile? BulkFile { get; set; }
}

public class BulkUploadCsvRecord
{
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }

	// Optional identity columns, present only in data-screening templates. Files
	// without these headers still parse - the reader tolerates missing fields.
	public string? DateOfBirth { get; set; }
	public string? SSSNumber { get; set; }
	public string? TINNumber { get; set; }
}