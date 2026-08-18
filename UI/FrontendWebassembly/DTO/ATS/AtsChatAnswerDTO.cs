namespace FrontendWebassembly.DTO.ATS;

public record AtsChatAnswerDTO(
	string Answer,
	IReadOnlyList<AtsOrderSummaryDTO>? Orders = null,
	AtsOrderDraftDTO? PendingDraft = null,
	string? Error = null);

public record AskAtsAssistantResponseDTO(AtsChatAnswerDTO Answer);

public record SearchOrdersBySubjectResponseDTO(IReadOnlyList<AtsOrderSummaryDTO> Orders);
