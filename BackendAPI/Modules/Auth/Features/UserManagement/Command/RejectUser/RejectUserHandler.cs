namespace Auth.Features.UserManagement.Command.RejectUser;
public record RejectUserCommand(Guid UserId) : ICommand<RejectUserResult>;
public record RejectUserResult(bool IsRejected);

public class RejectUserCommandValidator : AbstractValidator<RejectUserCommand>
{
	public RejectUserCommandValidator()
	{
		RuleFor(x => x.UserId)
			.NotEmpty().WithMessage("UserId is required.");
	}
}

public class RejectUserHandler : ICommandHandler<RejectUserCommand, RejectUserResult>
{
	private readonly IUserService _userService;

	public RejectUserHandler(IUserService userService)
	{
		_userService = userService;
	}

	public async Task<RejectUserResult> Handle(RejectUserCommand request, CancellationToken cancellationToken)
	{
		var rejected = await _userService.RejectUserAsync(request.UserId);
		return new RejectUserResult(rejected);
	}
}
