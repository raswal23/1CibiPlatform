namespace Auth.Features.UserManagement.Command.AddRole;
public record AddRoleCommand(AddRoleDTO role) : ICommand<AddRoleResult>;
public record AddRoleResult(bool isAdded);

public class AddRoleCommandValidator : AbstractValidator<AddRoleCommand>
{
	public AddRoleCommandValidator()
	{
		RuleFor(x => x.role)
			.NotNull().WithMessage("Role data is required.");
	}
}

public class AddRoleHandler : ICommandHandler<AddRoleCommand, AddRoleResult>
{
	private readonly IRoleService _roleService;

	public AddRoleHandler(IRoleService roleService)
	{
		_roleService = roleService;
	}
	public async Task<AddRoleResult> Handle(AddRoleCommand request, CancellationToken cancellationToken)
	{
		var addedApplication = await _roleService.AddRoleAsync(request.role);
		return new AddRoleResult(addedApplication);
	}

}




