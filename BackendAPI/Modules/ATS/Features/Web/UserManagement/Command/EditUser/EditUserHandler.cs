namespace ATS.Features.Web.UserManagement.Command.EditUser;

public record EditUserCommand(IReadOnlyCollection<EditUserDTO> editUsers) : ICommand<EditUserResult>;

public record EditUserResult(IReadOnlyList<UserDetailsDTO> users);

public class EditUserCommandValidator : AbstractValidator<EditUserCommand>
{
	public EditUserCommandValidator()
	{
		RuleFor(x => x.editUsers)
			.Cascade(CascadeMode.Stop)
			.NotEmpty().WithMessage("At least one user/module assignment is required.")
			.Must(HaveConsistentUserDetails)
			.WithMessage("All module assignments must contain the same user, client, and role details.");

		RuleForEach(x => x.editUsers).ChildRules(user =>
		{
			user.RuleFor(x => x.UserId)
				.NotEmpty().WithMessage("UserId is required.");
			user.RuleFor(x => x.ClientId)
				.GreaterThan(0).When(x => x.ClientId.HasValue)
				.WithMessage("ClientId must be greater than zero when provided.");
			user.RuleFor(x => x.Site)
				.NotEmpty().WithMessage("Site is required.")
				.MaximumLength(100).WithMessage("Site cannot exceed 100 characters.");
			user.RuleFor(x => x.RoleId)
				.GreaterThan(0).WithMessage("RoleId is required.");
			user.RuleFor(x => x.ModuleId)
				.GreaterThan(0).WithMessage("ModuleId is required.");
		});
	}

	private static bool HaveConsistentUserDetails(IReadOnlyCollection<EditUserDTO> users)
	{
		if (users.Count == 0)
			return true;

		var first = users.First();
		return users.All(user =>
			user.UserId == first.UserId &&
			user.IsActive == first.IsActive &&
			user.ClientId == first.ClientId &&
			user.Site == first.Site &&
			user.RoleId == first.RoleId);
	}
}

public class EditUserHandler : ICommandHandler<EditUserCommand, EditUserResult>
{
	private readonly IUserManagementService _userManagementService;

	public EditUserHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<EditUserResult> Handle(EditUserCommand request, CancellationToken cancellationToken)
	{
		var editedUser = await _userManagementService.EditUserAsync(request.editUsers, cancellationToken);
		return new EditUserResult(editedUser);
	}
}
