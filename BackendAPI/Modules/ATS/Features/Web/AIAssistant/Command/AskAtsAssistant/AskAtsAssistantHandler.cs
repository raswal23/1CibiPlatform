namespace ATS.Features.Web.AIAssistant.Command.AskAtsAssistant;

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
