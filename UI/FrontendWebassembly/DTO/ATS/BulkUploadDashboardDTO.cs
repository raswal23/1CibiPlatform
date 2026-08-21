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

	// Still queued or being sent. Distinguishes "16/17 because one failed" from
	// "16/17 because one has not gone out yet".
	public int EmailsPending { get; set; }
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

// The bulk file itself, as returned alongside the drill-down's first page.
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

// One subject created from a bulk file.
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

public record BulkUploadSubjectCountsDTO
{
	public long Total { get; set; }

	public long Pending { get; set; }

	public long Sent { get; set; }

	public long Failed { get; set; }
}

public record BulkUploadSubjectsResultDTO
{
	public BulkUploadHeaderDTO? File { get; set; }

	public KeysetPaginatedResult<BulkUploadSubjectListDTO>? Subjects { get; set; }
}

// Response envelopes, matching the property names the Carter endpoints return.
public record GetBulkUploadSubjectsResponseDTO
{
	public BulkUploadSubjectsResultDTO? Result { get; set; }
}

public record GetBulkUploadSubjectCountsResponseDTO
{
	public BulkUploadSubjectCountsDTO? Counts { get; set; }
}

// Mirrors ATS.Constants.BulkFileStatus, which lives in the backend assembly and is not
// referenced by the UI project.
public static class BulkUploadStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";
}

// Mirrors ATS.Constants.BulkSubjectEmailStatus - the caller-facing filter vocabulary,
// which is not the same as the EmailSentStatus values stored per row: a stored
// "Processing" is reported to the user as Pending.
public static class BulkSubjectEmailStatus
{
	public const string Pending = "Pending";

	public const string Sent = "Sent";

	public const string Failed = "Failed";
}

// The values actually stored in EmailInvitationRequest.EmailSentStatus, used to render
// a single row's badge.
public static class SubjectEmailSentStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	public const string Error = "Error";
}

// Mirrors ATS.Constants.ApplicationFormStatus.
public static class SubjectApplicationFormStatus
{
	public const string Pending = "Pending";

	public const string Withdrawn = "Withdrawn";

	public const string Done = "Done";
}
