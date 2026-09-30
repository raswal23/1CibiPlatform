namespace Auth.Features.UserManagement.Command.EditUserStatus;
public record EditUserStatusCommand(EditUserStatusDTO editUserStatus) : ICommand<EditUserStatusResult>;
public record EditUserStatusResult(UserDTO user);

public class EditUserStatusCommandValidator : AbstractValidator<EditUserStatusCommand>
{
	public EditUserStatusCommandValidator()
	{
		RuleFor(x => x.editUserStatus)
			.NotNull().WithMessage("Edit user status data is required.");

		When(x => x.editUserStatus != null, () =>
		{
			RuleFor(x => x.editUserStatus.UserId)
				.NotEmpty().WithMessage("UserId is required.");
		});
	}
}

public class EditUserStatusHandler : ICommandHandler<EditUserStatusCommand, EditUserStatusResult>
{
	private readonly IUserService _userService;

	public EditUserStatusHandler(IUserService userService)
	{
		_userService = userService;
	}

	public async Task<EditUserStatusResult> Handle(EditUserStatusCommand request, CancellationToken cancellationToken)
	{
		var editedUser = await _userService.EditUserStatusAsync(request.editUserStatus);
		return new EditUserStatusResult(editedUser);
	}
}
