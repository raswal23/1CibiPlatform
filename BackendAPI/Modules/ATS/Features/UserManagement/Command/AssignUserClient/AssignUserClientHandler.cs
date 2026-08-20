namespace ATS.Features.UserManagement.Command.AssignUserClient;

public record AssignUserClientCommand(AssignUserClientDTO assignment)
	: ICommand<AssignUserClientResult>;

public record AssignUserClientResult(UserClientDetailsDTO assignment);

public class AssignUserClientCommandValidator : AbstractValidator<AssignUserClientCommand>
{
	public AssignUserClientCommandValidator()
	{
		RuleFor(x => x.assignment)
			.NotNull().WithMessage("Client assignment is required.");

		RuleFor(x => x.assignment.UserId)
			.NotEmpty().WithMessage("An Auth user is required.");

		RuleFor(x => x.assignment.ClientId)
			.GreaterThan(0).WithMessage("ClientId is required.");
	}
}

public class AssignUserClientHandler
	: ICommandHandler<AssignUserClientCommand, AssignUserClientResult>
{
	private readonly IUserManagementService _userManagementService;

	public AssignUserClientHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<AssignUserClientResult> Handle(
		AssignUserClientCommand request,
		CancellationToken cancellationToken)
	{
		var assignment = await _userManagementService.AssignUserClientAsync(
			request.assignment,
			cancellationToken);
		return new AssignUserClientResult(assignment);
	}
}
