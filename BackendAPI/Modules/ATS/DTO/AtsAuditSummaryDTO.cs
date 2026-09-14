namespace ATS.DTO;

/// <summary>
/// One audit entry as the AI assistant reports it - who did what, where, and whether it
/// worked.
///
/// Deliberately narrower than <see cref="ATS.Data.DTO.AuditTrailListDTO"/>, which the audit
/// screen uses. Two fields are missing on purpose:
///
/// <c>Payload</c> and <c>Changes</c> are redacted but still rich JSON - the full command and
/// its field-level before/after values. Handing them to a language model widens the
/// prompt-injection surface (they contain free text a user typed) and the data-exposure
/// surface, for no gain over the audit screen, which already shows both in a detail panel
/// to the same super admins.
///
/// Identity columns (IpAddress, TraceId, AtsRoleId, AtsClientId) are omitted for the same
/// reason: the chat answers "what happened", and anything more forensic belongs on the
/// screen built for it.
/// </summary>
public record AtsAuditEntrySummaryDTO
{
	public DateTime OccurredAt { get; set; }

	public string Action { get; set; } = string.Empty;

	public string Area { get; set; } = string.Empty;

	public string Outcome { get; set; } = string.Empty;

	public string? UserFullName { get; set; }

	/// <summary>
	/// Present only on a failure. Truncated by the assistant before it reaches the model,
	/// because an exception message can be long and is attacker-influencable text.
	/// </summary>
	public string? FailureReason { get; set; }
}

/// <summary>
/// A rendered audit workbook, ready to stream. Mirrors <c>BulkUploadSubjectExportDTO</c>.
/// </summary>
public record AtsAuditExportDTO
{
	public Stream Content { get; set; } = Stream.Null;

	public string FileName { get; set; } = "audit-trail.xlsx";
}

/// <summary>
/// The filters the assistant used to produce an audit table, echoed back so the chat can
/// offer an "Export to Excel" button for exactly those rows.
///
/// This exists because a language model cannot hand the browser a file - a download has to
/// be started by a real user gesture on the page. So the model produces the QUERY and the
/// page produces the file, and this record is what carries the query between them.
///
/// Echoed from the arguments the model actually passed rather than re-derived, so the
/// export can never silently widen what the user was shown.
/// </summary>
public record AtsAuditQueryDTO
{
	public int DaysBack { get; set; }

	public string? Outcome { get; set; }

	public string? Action { get; set; }

	public string? Area { get; set; }

	public string? SearchTerm { get; set; }
}
