namespace ATS.Features.Web.OMSTicketing.Command.RetryTickets;

public record RetryTicketsCommand(IReadOnlyCollection<Guid> EmailInvitationIds)
	: ICommand<RetryTicketsResult>;

public record RetryTicketsResult(int RequestedCount, int RequeuedCount, bool IsComplete);

public class RetryTicketsCommandValidator : AbstractValidator<RetryTicketsCommand>
{
	public RetryTicketsCommandValidator()
	{
		RuleFor(x => x.EmailInvitationIds)
			.NotNull()
			.WithMessage("At least one order is required.")
			.Must(ids => ids is { Count: > 0 })
			.WithMessage("At least one order is required.");

		// The cap is enforced in the service too, because that is where the reason for it
		// lives. Validating here turns an oversized request into a 400 before it reaches a
		// database round trip.
		RuleFor(x => x.EmailInvitationIds)
			.Must(ids => ids is null || ids.Count <= OMSTicketingMonitoringService.MaxBulkRetrySize)
			.WithMessage(
				$"A bulk retry is limited to {OMSTicketingMonitoringService.MaxBulkRetrySize} orders at a time.");

		RuleForEach(x => x.EmailInvitationIds)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}

public class RetryTicketsHandler : ICommandHandler<RetryTicketsCommand, RetryTicketsResult>
{
	private readonly IOMSTicketingMonitoringService _ticketingMonitoringService;

	public RetryTicketsHandler(IOMSTicketingMonitoringService ticketingMonitoringService)
	{
		_ticketingMonitoringService = ticketingMonitoringService;
	}

	public async Task<RetryTicketsResult> Handle(
		RetryTicketsCommand request,
		CancellationToken cancellationToken)
	{
		var result = await _ticketingMonitoringService.RetryTicketsAsync(
			request.EmailInvitationIds,
			cancellationToken);

		return new RetryTicketsResult(
			result.RequestedCount,
			result.RequeuedCount,
			result.IsComplete);
	}
}
