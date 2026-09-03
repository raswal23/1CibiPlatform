namespace ATS.Features.Web.OMSTicketing.Command.RetryTicket;

public record RetryTicketCommand(Guid EmailInvitationId) : ICommand<RetryTicketResult>;

public record RetryTicketResult(bool Success);

public class RetryTicketCommandValidator : AbstractValidator<RetryTicketCommand>
{
	public RetryTicketCommandValidator()
	{
		RuleFor(x => x.EmailInvitationId)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}

public class RetryTicketHandler : ICommandHandler<RetryTicketCommand, RetryTicketResult>
{
	private readonly IOMSTicketingMonitoringService _ticketingMonitoringService;

	public RetryTicketHandler(IOMSTicketingMonitoringService ticketingMonitoringService)
	{
		_ticketingMonitoringService = ticketingMonitoringService;
	}

	public async Task<RetryTicketResult> Handle(
		RetryTicketCommand request,
		CancellationToken cancellationToken)
	{
		var success = await _ticketingMonitoringService.RetryTicketAsync(
			request.EmailInvitationId,
			cancellationToken);

		return new RetryTicketResult(success);
	}
}
