namespace ATS.Features.AIAssistant.Command.ConfirmOrderDraft;

public record ConfirmOrderDraftCommand(Guid DraftId) : ICommand<ConfirmOrderDraftResult>;

public record ConfirmOrderDraftResult(AtsChatAnswerDTO Answer);

public class ConfirmOrderDraftCommandValidator : AbstractValidator<ConfirmOrderDraftCommand>
{
	public ConfirmOrderDraftCommandValidator()
	{
		RuleFor(x => x.DraftId)
			.NotEmpty()
			.WithMessage("A draft id is required.");
	}
}

public class ConfirmOrderDraftHandler
	: ICommandHandler<ConfirmOrderDraftCommand, ConfirmOrderDraftResult>
{
	private readonly IAtsAssistantService _assistantService;

	public ConfirmOrderDraftHandler(IAtsAssistantService assistantService)
	{
		_assistantService = assistantService;
	}

	public async Task<ConfirmOrderDraftResult> Handle(
		ConfirmOrderDraftCommand request,
		CancellationToken cancellationToken)
	{
		var answer = await _assistantService.ConfirmOrderDraftAsync(
			request.DraftId,
			cancellationToken);

		return new ConfirmOrderDraftResult(answer);
	}
}
