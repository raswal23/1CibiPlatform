namespace FrontendWebassembly.Services.ATS.AIAssistant;

public interface IAtsAssistantService
{
	event Action<string>? ChatResponseReceived;

	event Action<bool>? TypingChanged;

	Task StartAsync();

	Task<AtsChatAnswerDTO> AskAsync(string question, CancellationToken cancellationToken = default);

	Task<AtsChatAnswerDTO> ConfirmOrderDraftAsync(
		Guid draftId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<AtsOrderSummaryDTO>> SearchOrdersBySubjectAsync(
		string name,
		CancellationToken cancellationToken = default);
}
