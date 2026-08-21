namespace FrontendWebassembly.DTO.ATS;

public record BulkUploadListDTO
{
	public Guid FileID { get; set; }

	public string? FileName { get; set; }

	public string? Requestor { get; set; }

	public string? PackageType { get; set; }

	public string? OrderType { get; set; }

	public string? Status { get; set; }

	public DateTime DateCreated { get; set; }

	public DateTime? ClaimedAt { get; set; }

	public int SubjectCount { get; set; }

	public int EmailsSent { get; set; }

	public int EmailsFailed { get; set; }
}

public record BulkUploadStatusCountsDTO
{
	public long Pending { get; set; }

	public long Processing { get; set; }

	public long Done { get; set; }

	public long Total { get; set; }
}

// Response envelopes, matching the property names the Carter endpoints return.
public record GetBulkUploadsResponseDTO
{
	public KeysetPaginatedResult<BulkUploadListDTO>? BulkUploads { get; set; }
}

public record GetBulkUploadStatusCountsResponseDTO
{
	public BulkUploadStatusCountsDTO? Counts { get; set; }
}

// Mirrors ATS.Constants.BulkFileStatus, which lives in the backend assembly and is not
// referenced by the UI project.
public static class BulkUploadStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";
}
