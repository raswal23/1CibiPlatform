namespace Auth.Features.UserManagement.Command.DeleteLockedUser;
public record DeleteLockedUserCommand(Guid LockedUserId) : ICommand<DeleteLockedUserResult>;
public record DeleteLockedUserResult(bool IsDeleted);

public class DeleteLockedUserCommandValidator : AbstractValidator<DeleteLockedUserCommand>
{
	public DeleteLockedUserCommandValidator()
	{
		RuleFor(x => x.LockedUserId)
			.NotEmpty().WithMessage("Locked user id is required.");
	}
}

public class DeleteLockedUserHandler : ICommandHandler<DeleteLockedUserCommand, DeleteLockedUserResult>
{
	private readonly ILockerUserService _lockedUserService;

	public DeleteLockedUserHandler(ILockerUserService lockedUserService)
	{
		_lockedUserService = lockedUserService;
	}
	public async Task<DeleteLockedUserResult> Handle(DeleteLockedUserCommand request, CancellationToken cancellationToken)
	{
		var deletedLockedRole = await _lockedUserService.DeleteLockedUserAsync(request.LockedUserId);
		return new DeleteLockedUserResult(deletedLockedRole);
	}
}


