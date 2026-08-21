namespace ATS.Data.DTO;

// Repository projection for the bulk upload dashboard. DateCreated and FileID are the
// keyset sort keys, so both must survive the projection to mint the next cursor.
public record BulkUploadRowDTO
{
	public Guid FileID { get; set; }

	public string? FileName { get; set; }

	public string? Requestor { get; set; }

	public string? PackageType { get; set; }

	public string? OrderType { get; set; }

	public string? Status { get; set; }

	public DateTime DateCreated { get; set; }

	public DateTime? ClaimedAt { get; set; }
}

// The wire shape: the upload file plus the rolled-up progress of the invitations the
// bulk submission job created from it.
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

// Per-file invitation rollup, fetched once for a whole page rather than per row.
public record BulkFileInvitationRollupDTO
{
	public Guid FileID { get; set; }

	public int SubjectCount { get; set; }

	public int EmailsSent { get; set; }

	public int EmailsFailed { get; set; }
}
