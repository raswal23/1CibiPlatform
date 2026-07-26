namespace ATS.Features.MarkAsDisputed;

public record MarkAsDisputedCommand(DisputeOrderRequestDTO disputeRequest) : ICommand<MarkAsDisputedResult>;

public record MarkAsDisputedResult(bool Success);

public class MarkAsDisputedCommandValidator : AbstractValidator<MarkAsDisputedCommand>
{
	public MarkAsDisputedCommandValidator()
	{
		RuleFor(x => x.disputeRequest.EmailInvitationId)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");

		RuleFor(x => x.disputeRequest.Company)
			.NotEmpty()
			.WithMessage("Company is required.")
			.MaximumLength(255)
			.WithMessage("Company must not exceed 255 characters.");

		RuleFor(x => x.disputeRequest.DisputeReason)
			.NotEmpty()
			.WithMessage("Dispute reason is required.")
			.MaximumLength(255)
			.WithMessage("Dispute reason must not exceed 255 characters.");

		RuleFor(x => x.disputeRequest.OrderCreatedAt)
			.NotNull()
			.WithMessage("Order created date is required.");
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
		var success = await _disputeOrderService.MarkAsDisputedAsync(request.disputeRequest, cancellationToken);
		return new MarkAsDisputedResult(success);
	}
}
