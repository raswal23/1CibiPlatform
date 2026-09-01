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

	// Still queued or claimed by the email job. Without this the UI cannot tell
	// "16/17 because one failed" from "16/17 because one has not been sent yet".
	public int EmailsPending { get; set; }
}

// One CSV row the parser refused, reported back to the uploader. The file is parsed
// asynchronously, so these are persisted on the file rather than returned inline.
public record BulkUploadRejectedRowDTO
{
	public int RowNumber { get; set; }

	public string Reason { get; set; } = string.Empty;
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

	public int EmailsPending { get; set; }
}

// The bulk file itself, returned by the drill-down so the dialog renders its header
// from the server rather than trusting the row the caller happened to click.
// A null result from the repository means "not visible to this caller", which the
// service turns into a 404 - an empty subject list would be indistinguishable from a
// file that simply has not been parsed yet.
public record BulkUploadHeaderDTO
{
	public Guid FileID { get; set; }

	public string? FileName { get; set; }

	public string? Requestor { get; set; }

	public string? PackageType { get; set; }

	public string? OrderType { get; set; }

	public string? Status { get; set; }

	public DateTime DateCreated { get; set; }
}

// One invitation created from a bulk file. EmailInvitationID is the keyset sort key.
public record BulkUploadSubjectListDTO
{
	public Guid EmailInvitationID { get; set; }

	public string? LastName { get; set; }

	public string? FirstName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? EmailAddress { get; set; }

	public string? MobileNumber { get; set; }

	public string? EmailSentStatus { get; set; }

	public DateTime? EmailSentAt { get; set; }

	public int EmailSendAttempts { get; set; }

	public DateTime? EmailClaimedAt { get; set; }

	public string? ApplicationFormStatus { get; set; }

	public DateTime? FormCompletedAt { get; set; }

	public string? OrderStatus { get; set; }
}

// Chip counts for one file's subjects. Pending deliberately spans two stored values -
// see BulkSubjectEmailStatus for why the job's Pending/Processing split is not exposed.
public record BulkUploadSubjectCountsDTO
{
	public long Total { get; set; }

	public long Pending { get; set; }

	public long Sent { get; set; }

	public long Failed { get; set; }
}

// The CSV export. The download filename is derived from the stored file name on the
// server, so the endpoint never echoes a caller-supplied value into Content-Disposition.
public record BulkUploadSubjectExportDTO
{
	public Stream Content { get; set; } = Stream.Null;

	public string FileName { get; set; } = "subjects.csv";
}

// The drill-down's wire shape: the file header travels with the page so the dialog
// needs one round trip, not two.
public record BulkUploadSubjectsResultDTO
{
	public BulkUploadHeaderDTO File { get; set; } = new();

	public KeysetPaginatedResult<BulkUploadSubjectListDTO> Subjects { get; set; } =
		new(Array.Empty<BulkUploadSubjectListDTO>(), null, 0);
}
