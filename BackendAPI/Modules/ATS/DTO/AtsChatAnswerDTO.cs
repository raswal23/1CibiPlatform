namespace ATS.DTO;

/// <summary>
/// The single response contract for the ATS assistant. The UI renders whichever
/// parts are present: markdown prose, an order table, an audit table, and/or a
/// pending order confirmation card.
/// </summary>
public record AtsChatAnswerDTO(
	string Answer,
	IReadOnlyList<AtsOrderSummaryDTO>? Orders = null,
	AtsOrderDraftDTO? PendingDraft = null,
	IReadOnlyList<AtsAuditEntrySummaryDTO>? AuditEntries = null,
	AtsAuditQueryDTO? AuditQuery = null,
	string? Error = null);
