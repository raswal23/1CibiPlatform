namespace ATS.Features.Web.AIAssistant.Command.AskAtsAssistant;

// Still skipped by the PIPELINE, but no longer unaudited: AtsAssistantService writes its
// own entry instead.
//
// The pipeline only ever serializes the request, so an entry written here would record the
// question and lose the answer - and half a conversation is not a record of it. The service
// has both sides, so it records the exchange itself. See AtsAssistantService.RecordAudit.
[SkipAudit]
public record AskAtsAssistantCommand(string Question) : ICommand<AskAtsAssistantResult>;

public record AskAtsAssistantResult(AtsChatAnswerDTO Answer);

public class AskAtsAssistantCommandValidator : AbstractValidator<AskAtsAssistantCommand>
{
	public AskAtsAssistantCommandValidator()
	{
		RuleFor(x => x.Question)
			.NotEmpty()
			.WithMessage("A question is required.")
			.MaximumLength(2000)
			.WithMessage("The question must not exceed 2000 characters.");
	}
}

public class AskAtsAssistantHandler : ICommandHandler<AskAtsAssistantCommand, AskAtsAssistantResult>
{
	private readonly IAtsAssistantService _assistantService;

	public AskAtsAssistantHandler(IAtsAssistantService assistantService)
	{
		_assistantService = assistantService;
	}

	public async Task<AskAtsAssistantResult> Handle(
		AskAtsAssistantCommand request,
		CancellationToken cancellationToken)
	{
		var answer = await _assistantService.AskAsync(request.Question, cancellationToken);

		return new AskAtsAssistantResult(answer);
	}
}
