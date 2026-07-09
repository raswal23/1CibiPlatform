namespace Auth.Features.UserManagement.Command.EditUser;
public record EditUserCommand(EditUserDTO editUser) : ICommand<EditUserResult>;
public record EditUserResult(UserDTO user);

public class EditUserCommandValidator : AbstractValidator<EditUserCommand>
{
	public EditUserCommandValidator()
	{
		RuleFor(x => x.editUser)
			.NotNull().WithMessage("Edit user data is required.");

		When(x => x.editUser != null, () =>
		{
			RuleFor(x => x.editUser.Email)
				.NotEmpty().WithMessage("Email is required.");
			RuleFor(x => x.editUser.IsApproved)
				.NotNull().WithMessage("IsApproved is required.");
		});
	}
}

public class EditUserHandler : ICommandHandler<EditUserCommand, EditUserResult>
{
	private readonly IUserService _userService;

	public EditUserHandler(IUserService userService)
	{
		_userService = userService;
	}
	public async Task<EditUserResult> Handle(EditUserCommand request, CancellationToken cancellationToken)
	{
		var editedUser = await _userService.EditUserAsync(request.editUser);
		return new EditUserResult(editedUser);
	}
}
