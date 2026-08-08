namespace ATS.Features.UserManagement.Command.AddUser;

public record AddUserCommand(IReadOnlyCollection<AddUserDTO> users) : ICommand<AddUserResult>;

public record AddUserResult(bool isAdded);

public class AddUserCommandValidator : AbstractValidator<AddUserCommand>
{
	public AddUserCommandValidator()
	{
		RuleFor(x => x.users)
			.Cascade(CascadeMode.Stop)
			.NotEmpty().WithMessage("At least one user/module assignment is required.")
			.Must(HaveConsistentUserDetails)
			.WithMessage("All module assignments must contain the same user, client, and role details.");

		RuleForEach(x => x.users).ChildRules(user =>
		{
			user.RuleFor(x => x.UserId)
				.NotEmpty().WithMessage("An Auth user is required.");
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

	private static bool HaveConsistentUserDetails(IReadOnlyCollection<AddUserDTO> users)
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

public class AddUserHandler : ICommandHandler<AddUserCommand, AddUserResult>
{
	private readonly IUserManagementService _userManagementService;

	public AddUserHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<AddUserResult> Handle(AddUserCommand request, CancellationToken cancellationToken)
	{
		var addedUser = await _userManagementService.AddUserAsync(request.users, cancellationToken);
		return new AddUserResult(addedUser);
	}
}
