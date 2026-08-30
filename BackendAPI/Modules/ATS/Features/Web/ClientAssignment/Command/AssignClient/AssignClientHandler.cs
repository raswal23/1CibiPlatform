namespace ATS.Features.Web.ClientAssignment.Command.AssignClient;

public record AssignClientCommand(AssignUserClientDTO Assignment)
	: ICommand<AssignClientResult>;

public record AssignClientResult(ClientAssignmentDetailsDTO Assignment);

public sealed class AssignClientCommandValidator : AbstractValidator<AssignClientCommand>
{
	public AssignClientCommandValidator()
	{
		RuleFor(command => command.Assignment)
			.NotNull();
		RuleFor(command => command.Assignment.UserId)
			.NotEmpty();
		RuleFor(command => command.Assignment.ClientId)
			.GreaterThan(0);
	}
}

public sealed class AssignClientHandler
	: ICommandHandler<AssignClientCommand, AssignClientResult>
{
	private readonly IClientAssignmentService _clientAssignmentService;

	public AssignClientHandler(IClientAssignmentService clientAssignmentService)
	{
		_clientAssignmentService = clientAssignmentService;
	}

	public async Task<AssignClientResult> Handle(
		AssignClientCommand request,
		CancellationToken cancellationToken)
	{
		var assignment = await _clientAssignmentService.AssignClientAsync(
			request.Assignment,
			cancellationToken);
		return new AssignClientResult(assignment);
	}
}
