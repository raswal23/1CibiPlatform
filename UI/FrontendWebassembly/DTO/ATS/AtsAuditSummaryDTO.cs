namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// One audit entry as the AI assistant reports it. Mirrors the API's
/// <c>AtsAuditEntrySummaryDTO</c>.
///
/// Deliberately narrower than the audit screen's row: the redacted payload and the
/// field-level changes are not carried here, because they are not sent to the assistant at
/// all. Read them on the Audit Trail screen's detail panel instead.
/// </summary>
public record AtsAuditEntrySummaryDTO
{
	public DateTime OccurredAt { get; set; }

	public string Action { get; set; } = string.Empty;

	public string Area { get; set; } = string.Empty;

	public string Outcome { get; set; } = string.Empty;

	public string? UserFullName { get; set; }

	public string? FailureReason { get; set; }
}

/// <summary>
/// The filters behind an assistant-produced audit table, so the chat can offer an export of
/// exactly those rows.
///
/// The assistant cannot hand the browser a file - a download has to be started by a real
/// user gesture on the page. So the model produces the query and the page produces the
/// file; this carries the query between them.
/// </summary>
public record AtsAuditQueryDTO
{
	public int DaysBack { get; set; }

	public string? Outcome { get; set; }

	public string? Action { get; set; }

	public string? Area { get; set; }

	public string? SearchTerm { get; set; }
}
