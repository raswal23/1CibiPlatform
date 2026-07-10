namespace ATS.Features.ResendApplicationForm;

public record ResendApplicationFormCommand(Guid EmailInvitationId) : ICommand<ResendApplicationFormResult>;

public record ResendApplicationFormResult(bool Success);

public class ResendApplicationFormCommandValidator : AbstractValidator<ResendApplicationFormCommand>
{
	public ResendApplicationFormCommandValidator()
	{
		RuleFor(x => x.EmailInvitationId)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}

public class ResendApplicationFormCommandHandler : ICommandHandler<ResendApplicationFormCommand, ResendApplicationFormResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;

	public ResendApplicationFormCommandHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}

	public async Task<ResendApplicationFormResult> Handle(ResendApplicationFormCommand request, CancellationToken cancellationToken)
	{
		var success = await _endorsementSubmissionService.ResendApplicationFormAsync(request.EmailInvitationId, cancellationToken);
		return new ResendApplicationFormResult(success);
	}
}
