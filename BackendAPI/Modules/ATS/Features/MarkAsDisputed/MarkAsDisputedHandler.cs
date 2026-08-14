namespace ATS.Features.MarkAsDisputed;

public record MarkAsDisputedCommand(DisputeOrderRequestDTO DisputeRequest) : ICommand<MarkAsDisputedResult>;

public record MarkAsDisputedResult(bool Success);

public class MarkAsDisputedCommandValidator : AbstractValidator<MarkAsDisputedCommand>
{
	public MarkAsDisputedCommandValidator()
	{
		RuleFor(x => x.DisputeRequest.EmailInvitationId)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");

		RuleFor(x => x.DisputeRequest.DisputeReason)
			.NotEmpty()
			.WithMessage("Dispute reason is required.")
			.MaximumLength(255)
			.WithMessage("Dispute reason must not exceed 255 characters.");
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
		var success = await _disputeOrderService.MarkAsDisputedAsync(
			request.DisputeRequest,
			cancellationToken);
		return new MarkAsDisputedResult(success);
	}
}
