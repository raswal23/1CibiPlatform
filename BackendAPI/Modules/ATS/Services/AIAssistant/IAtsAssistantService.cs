namespace ATS.Services.AIAssistant;

public interface IAtsAssistantService
{
	Task<AtsChatAnswerDTO> AskAsync(string question, CancellationToken cancellationToken);

	Task<AtsChatAnswerDTO> ConfirmOrderDraftAsync(Guid draftId, CancellationToken cancellationToken);

	Task<IReadOnlyList<AtsOrderSummaryDTO>> SearchOrdersBySubjectAsync(
		string name,
		CancellationToken cancellationToken);
}
