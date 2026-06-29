namespace FrontendWebassembly.Services.AIAgentChat.Interface;

public interface IAIAgentChatService
{
	// Events raised by the implementation when SignalR messages arrive
	event Action<string> AiResponseReceived;
	event Action<bool> TypingStatusChanged;

	// Ask AI with optional file attachment and explicit skill selection
	Task<AIAnswerDTO> AskAIAsync(
		string question,
		IBrowserFile? file,
		string? explicitSkillName,
		CancellationToken cancellationToken);
}

