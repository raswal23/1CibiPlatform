namespace ATS.DTO;

/// <summary>
/// The single response contract for the ATS assistant. The UI renders whichever
/// parts are present: markdown prose, a result table, and/or a pending order
/// confirmation card.
/// </summary>
public record AtsChatAnswerDTO(
	string Answer,
	IReadOnlyList<AtsOrderSummaryDTO>? Orders = null,
	AtsOrderDraftDTO? PendingDraft = null,
	string? Error = null);
