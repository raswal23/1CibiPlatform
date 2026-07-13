namespace ATS.Features.MarkAsDisputed;

public record MarkAsDisputedCommand(Guid EmailInvitationId) : ICommand<MarkAsDisputedResult>;

public record MarkAsDisputedResult(bool Success);

public class MarkAsDisputedCommandValidator : AbstractValidator<MarkAsDisputedCommand>
{
	public MarkAsDisputedCommandValidator()
	{
		RuleFor(x => x.EmailInvitationId)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}

public class MarkAsDisputedCommandHandler : ICommandHandler<MarkAsDisputedCommand, MarkAsDisputedResult>
{
	private readonly IDisputeOrderService _disputeOrderService;

	public MarkAsDisputedCommandHandler(IDisputeOrderService disputeOrderService)
	{
		_disputeOrderService = disputeOrderService;
	}

	public async Task<MarkAsDisputedResult> Handle(MarkAsDisputedCommand request, CancellationToken cancellationToken)
	{
		var success = await _disputeOrderService.MarkAsDisputedAsync(request.EmailInvitationId, cancellationToken);
		return new MarkAsDisputedResult(success);
	}
}
